#include "pch.h"
#include "Common/apihook.h"
#include "Application.h"
#include "ProcessManipulation.h"
#include "Injector.h"

InjectLibrary::Injector injector("L2BotHookMutex", WH_CALLWNDPROC);
Application application(VersionAbstractFactory::Version::interlude);
HANDLE g_StopEvent = NULL;
HANDLE g_WorkerThread = NULL;
HMODULE g_Module = NULL;
bool g_IsLineageProcess = false;
int g_ProcessId = 0;

void ConfigLogger(HMODULE hModule)
{
	wchar_t buf[MAX_PATH];
	GetModuleFileNameW(hModule, buf, MAX_PATH);
	const std::wstring libName(buf);

	std::wstring directory;
	const size_t lastSlashIndex = libName.rfind(L"\\");
	if (std::string::npos != lastSlashIndex)
	{
		directory = libName.substr(0, lastSlashIndex);
	}

	std::vector <std::unique_ptr<Logger::LogChannel>> channels;
#ifdef _DEBUG
	channels.push_back(std::make_unique<OutputDebugLogChannel>(std::vector<Logger::LogLevel>{}));
#endif
	channels.push_back(std::make_unique<FileLogChannel>(directory + L"\\app.log", std::vector<Logger::LogLevel>{
#ifndef _DEBUG
		Logger::LogLevel::error,
		Logger::LogLevel::warning
#endif
	}));
	channels.push_back(std::make_unique<ChatLogChannel>(Enums::ChatChannelEnum::log, std::vector<Logger::LogLevel>{
		Logger::LogLevel::app
	}));
	Services::ServiceLocator::GetInstance().SetLogger(std::make_unique<Logger::Logger>(std::move(channels)));
}

DWORD WINAPI RuntimeThread(LPVOID)
{
	if (!g_IsLineageProcess)
	{
		return 0;
	}

	ConfigLogger(g_Module);

	try
	{
		InjectLibrary::StopCurrentProcess();
		application.Start();
		InjectLibrary::StartCurrentProcess();
		Services::ServiceLocator::GetInstance().GetLogger()->Info(L"attached to Lineage 2 client {:#010x}", g_ProcessId);
	}
	catch (...)
	{
		InjectLibrary::StartCurrentProcess();
	}

	WaitForSingleObject(g_StopEvent, INFINITE);

	try
	{
		InjectLibrary::StopCurrentProcess();
		application.Stop();
		InjectLibrary::StartCurrentProcess();
		Services::ServiceLocator::GetInstance().GetLogger()->Info(L"detached from Lineage 2 client {:#010x}", g_ProcessId);
	}
	catch (...)
	{
		InjectLibrary::StartCurrentProcess();
	}

	return 0;
}

BOOL APIENTRY DllMain(HMODULE hModule,
	DWORD  ul_reason_for_call,
	LPVOID lpReserved
)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
		DisableThreadLibraryCalls(hModule);
		g_Module = hModule;
		g_ProcessId = GetCurrentProcessId();
		g_IsLineageProcess = InjectLibrary::GetCurrentProcessName() == "l2.exe";

		injector.SetHook(hModule);
		if (g_IsLineageProcess) {
			g_StopEvent = CreateEventW(NULL, TRUE, FALSE, NULL);
			if (g_StopEvent != NULL)
			{
				g_WorkerThread = CreateThread(NULL, 0, RuntimeThread, NULL, 0, NULL);
			}
		}
		break;
	case DLL_PROCESS_DETACH:
		if (g_IsLineageProcess) {
			if (g_StopEvent != NULL)
			{
				SetEvent(g_StopEvent);
			}

			if (lpReserved == NULL && g_WorkerThread != NULL)
			{
				WaitForSingleObject(g_WorkerThread, 5000);
			}

			if (g_WorkerThread != NULL)
			{
				CloseHandle(g_WorkerThread);
				g_WorkerThread = NULL;
			}
			if (g_StopEvent != NULL)
			{
				CloseHandle(g_StopEvent);
				g_StopEvent = NULL;
			}
		}
		injector.SetHook();
		break;
	}
	return TRUE;
}
