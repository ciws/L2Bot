#pragma once

#include <Windows.h>
#include <string>
#include <memory>
#include <cstdint>
#include <atomic>
#include <format>
#include <mutex>
#include "Domain/Exceptions.h"
#include "Domain/Services/ServiceLocator.h"

#define BUFFER_SIZE 16384

class NamedPipe
{
public:
	const bool Connect(const std::wstring& pipeName)
	{
		std::scoped_lock guard(m_WriteMutex, m_ReadMutex);
		if (m_StopRequested) return false;
		const bool recreatePipe = m_Pipe == NULL || m_Pipe == INVALID_HANDLE_VALUE || m_PipeName != pipeName;
		m_PipeName = pipeName;

		if (recreatePipe)
		{
			if (m_Pipe != NULL && m_Pipe != INVALID_HANDLE_VALUE)
			{
				DisconnectNamedPipe(m_Pipe);
				CloseHandle(m_Pipe);
				m_Pipe = NULL;
			}

			CreateOverlapped(m_ConntectingOverlapped);
			CreateOverlapped(m_ReadingOverlapped);
			CreateOverlapped(m_WritingOverlapped);

			m_Pipe = CreateNamedPipeW((L"\\\\.\\pipe\\" + pipeName).c_str(),
				PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED,
				PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
				PIPE_UNLIMITED_INSTANCES,
				BUFFER_SIZE * sizeof(wchar_t),
				BUFFER_SIZE * sizeof(wchar_t),
				NMPWAIT_USE_DEFAULT_WAIT,
				NULL
			);

			if (m_Pipe == INVALID_HANDLE_VALUE)
			{
				throw CriticalRuntimeException(std::format(L"cannot create the pipe {}: {}", pipeName, GetLastError()));
			}
		}
		else
		{
			DisconnectNamedPipe(m_Pipe);
		}

		ResetEvent(m_ConntectingOverlapped.hEvent);
		const auto result = ConnectNamedPipe(m_Pipe, &m_ConntectingOverlapped);
		if (result)
		{
			m_Connected = true;
			return true;
		}

		const auto lastError = GetLastError();
		if (lastError == ERROR_PIPE_CONNECTED)
		{
			SetEvent(m_ConntectingOverlapped.hEvent);
			m_Connected = true;
			return true;
		}
		if (lastError != ERROR_IO_PENDING)
		{
			throw CriticalRuntimeException(std::format(L"cannot connect the pipe {}: {}", m_PipeName, lastError));
		}

		while (!m_StopRequested)
		{
			const auto waitResult = WaitForSingleObject(m_ConntectingOverlapped.hEvent, 250);
			if (waitResult == WAIT_TIMEOUT)
			{
				continue;
			}
			if (waitResult != WAIT_OBJECT_0)
			{
				throw CriticalRuntimeException(std::format(L"an error has occurred when connecting to the pipe {}: {}", m_PipeName, GetLastError()));
			}

			DWORD ret = 0;
			const auto overlappedResult = GetOverlappedResult(m_Pipe, &m_ConntectingOverlapped, &ret, FALSE);
			if (!overlappedResult)
			{
				const auto overlappedError = GetLastError();
				if (overlappedError == ERROR_OPERATION_ABORTED || overlappedError == ERROR_BROKEN_PIPE || overlappedError == ERROR_NO_DATA || m_StopRequested)
				{
					m_Connected = false;
					return false;
				}

				throw CriticalRuntimeException(std::format(L"cannot get overlapped result for the pipe {} when connecting: {}", m_PipeName, overlappedError));
			}

			m_Connected = true;
			return true;
		}

		m_Connected = false;
		return false;
	}

	void Send(const std::wstring& message)
	{
		std::lock_guard<std::mutex> guard(m_WriteMutex);
		if (!m_Connected)
		{
			return;
		}

		const std::wstring preparedMessage = message + L"\n";

		ResetEvent(m_WritingOverlapped.hEvent);
		DWORD written;
		const auto result = WriteFile(m_Pipe, preparedMessage.c_str(), static_cast<DWORD>((preparedMessage.size() + 1) * sizeof(wchar_t)), &written, &m_WritingOverlapped);

		const auto lastError = GetLastError();
		if (!result && (lastError == ERROR_BROKEN_PIPE || lastError == ERROR_NO_DATA))
		{
			m_Connected = false;
			return;
		}
		if (!result)
		{
			if (lastError == ERROR_IO_PENDING)
			{
				while (!m_StopRequested)
				{
					const auto waitResult = WaitForSingleObject(m_WritingOverlapped.hEvent, 250);
					if (waitResult == WAIT_TIMEOUT)
					{
						continue;
					}
					if (waitResult != WAIT_OBJECT_0)
					{
						m_Connected = false;
						throw CriticalRuntimeException(std::format(L"cannot wait for writing to the pipe {}: {}", m_PipeName, GetLastError()));
					}

					DWORD ret = 0;
					const auto overlappedResult = GetOverlappedResult(m_Pipe, &m_WritingOverlapped, &ret, FALSE);
					if (!overlappedResult)
					{
						const auto overlappedError = GetLastError();
						if (overlappedError == ERROR_OPERATION_ABORTED || overlappedError == ERROR_BROKEN_PIPE || overlappedError == ERROR_NO_DATA || m_StopRequested)
						{
							m_Connected = false;
							return;
						}

						m_Connected = false;
						throw CriticalRuntimeException(std::format(L"cannot get overlapped result for the pipe {} when writing: {}", m_PipeName, overlappedError));
					}

					return;
				}

				m_Connected = false;
				return;
			}
			else
			{
				m_Connected = false;
				throw CriticalRuntimeException(std::format(L"cannot write to the pipe {}: {}", m_PipeName, lastError));
			}
		}
	}

	const std::wstring Receive()
	{
		std::lock_guard<std::mutex> guard(m_ReadMutex);
		if (!m_Connected || m_StopRequested)
		{
			return L"";
		}

		DWORD dwRead = 0;
		std::unique_ptr<wchar_t[]> buffer = std::make_unique<wchar_t[]>(BUFFER_SIZE);
		ResetEvent(m_ReadingOverlapped.hEvent);
		const auto result = ReadFile(m_Pipe, buffer.get(), BUFFER_SIZE * sizeof(wchar_t), &dwRead, &m_ReadingOverlapped);

		const auto lastError = GetLastError();
		if (!result)
		{
			if (lastError == ERROR_IO_PENDING)
			{
				while (!m_StopRequested)
				{
					const auto waitResult = WaitForSingleObject(m_ReadingOverlapped.hEvent, 250);
					if (waitResult == WAIT_TIMEOUT)
					{
						continue;
					}
					if (waitResult != WAIT_OBJECT_0)
					{
						m_Connected = false;
						throw CriticalRuntimeException(std::format(L"cannot wait for reading from the pipe {}: {}", m_PipeName, GetLastError()));
					}

					const auto overlappedResult = GetOverlappedResult(m_Pipe, &m_ReadingOverlapped, &dwRead, FALSE);
					if (!overlappedResult)
					{
						const auto overlappedError = GetLastError();
						if (overlappedError == ERROR_OPERATION_ABORTED || m_StopRequested || overlappedError == ERROR_BROKEN_PIPE)
						{
							m_Connected = false;
							return L"";
						}

						m_Connected = false;
						throw CriticalRuntimeException(std::format(L"cannot get overlapped result for the pipe {} when reading: {}", m_PipeName, overlappedError));
					}

					break;
				}

				if (m_StopRequested)
				{
					m_Connected = false;
					return L"";
				}
			}
			else
			{
				if (lastError == ERROR_BROKEN_PIPE || lastError == ERROR_NO_DATA)
				{
					m_Connected = false;
					return L"";
				}
				m_Connected = false;
				throw CriticalRuntimeException(std::format(L"cannot read from the pipe {}: {}", m_PipeName, lastError));
			}
		}

		std::wstring message(buffer.get(), dwRead / sizeof(wchar_t));
		while (!message.empty() && message.back() == L'\0') message.pop_back();

		return message;
	}

	const bool IsConnected() const
	{
		return m_Connected;
	}

	void RequestStop()
	{
		m_StopRequested = true;
		m_Connected = false;

		if (m_Pipe != NULL && m_Pipe != INVALID_HANDLE_VALUE)
		{
			CancelIoEx(m_Pipe, &m_ConntectingOverlapped);
			CancelIoEx(m_Pipe, &m_ReadingOverlapped);
			CancelIoEx(m_Pipe, &m_WritingOverlapped);
		}

		if (m_ConntectingOverlapped.hEvent != NULL)
		{
			SetEvent(m_ConntectingOverlapped.hEvent);
		}
		if (m_ReadingOverlapped.hEvent != NULL)
		{
			SetEvent(m_ReadingOverlapped.hEvent);
		}
		if (m_WritingOverlapped.hEvent != NULL)
		{
			SetEvent(m_WritingOverlapped.hEvent);
		}
	}

	virtual ~NamedPipe()
	{
		RequestStop();

		if (m_ConntectingOverlapped.hEvent != NULL)
		{
			CloseHandle(m_ConntectingOverlapped.hEvent);
			m_ConntectingOverlapped.hEvent = NULL;
		}
		if (m_ReadingOverlapped.hEvent != NULL)
		{
			CloseHandle(m_ReadingOverlapped.hEvent);
			m_ReadingOverlapped.hEvent = NULL;
		}
		if (m_WritingOverlapped.hEvent != NULL)
		{
			CloseHandle(m_WritingOverlapped.hEvent);
			m_WritingOverlapped.hEvent = NULL;
		}
		if (m_Pipe != NULL && m_Pipe != INVALID_HANDLE_VALUE)
		{
			CloseHandle(m_Pipe);
			m_Pipe = NULL;
		}
	}
	NamedPipe() = default;

private:
	void CreateOverlapped(OVERLAPPED& overlapped)
	{
		if (overlapped.hEvent == NULL)
		{
			overlapped.hEvent = CreateEvent(NULL, TRUE, FALSE, NULL);
			if (overlapped.hEvent == NULL)
			{
				throw CriticalRuntimeException(std::format(L"cannot create overlapped for the pipe {}: {}", m_PipeName, GetLastError()));
			}
			overlapped.Offset = 0;
			overlapped.OffsetHigh = 0;
		}
	}

private:
	std::wstring m_PipeName = L"";
	HANDLE m_Pipe = NULL;
	std::atomic_bool m_Connected = false;
	std::mutex m_WriteMutex;
	std::mutex m_ReadMutex;
	std::atomic_bool m_StopRequested = false;
	OVERLAPPED m_ConntectingOverlapped = OVERLAPPED();
	OVERLAPPED m_ReadingOverlapped = OVERLAPPED();
	OVERLAPPED m_WritingOverlapped = OVERLAPPED();
};
