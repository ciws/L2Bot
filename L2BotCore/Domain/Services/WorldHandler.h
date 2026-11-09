#pragma once

#include <cstdint>
#include <thread>
#include <memory>
#include <atomic>
#include <mutex>
#include <chrono>
#include "WorldState.h"
#include "../Serializers/SerializerInterface.h"
#include "../Repositories/EntityRepositoryInterface.h"
#include "../Transports/TransportInterface.h"
#include "../DTO/Message.h"
#include "../Services/IncomingMessageProcessor.h"
#include "../Services/OutgoingMessageBuilder.h"
#include "../Exceptions.h"
#include "../Services/ServiceLocator.h"

namespace L2Bot::Domain::Services
{
	class WorldHandler
	{
	public:
		WorldHandler(
			const std::map<std::wstring, Repositories::EntityRepositoryInterface&> repositories,
			const Serializers::SerializerInterface& serializer,
			const Services::IncomingMessageProcessor& incomingMessageProcessor,
			Transports::TransportInterface& transport
		) :
			m_Repositories(repositories),
			m_Serializer(serializer),
			m_IncomingMessageProcessor(incomingMessageProcessor),
			m_Transport(transport)
		{

		}

		void Start()
		{
			m_Stopped = false;
			for (const auto& kvp : m_Repositories)
			{
				kvp.second.Init();
			}

			m_ConnectingThread = std::thread(&WorldHandler::Connect, this);
			m_SendingThread = std::thread(&WorldHandler::Send, this);
			m_ReceivingThread = std::thread(&WorldHandler::Receive, this);
		}

		void Stop()
		{
			m_Stopped.store(true);
			m_Transport.Stop();
			if (m_ConnectingThread.joinable())
			{
				m_ConnectingThread.join();
			}
			if (m_SendingThread.joinable())
			{
				m_SendingThread.join();
			}
			if (m_ReceivingThread.joinable())
			{
				m_ReceivingThread.join();
			}
		}

		virtual ~WorldHandler() = default;

	private:
		void Send()
		{
			while (!m_Stopped.load())
			{
				try {
					if (m_ClientReady && m_SnapshotReady && m_Transport.IsConnected())
					{
						for (const auto& message : GetOutgoingMessages())
						{
							m_Transport.Send(
								message
							);
						}
					}

					std::this_thread::sleep_for(std::chrono::milliseconds(50));
				}
				catch (const CriticalRuntimeException& e)
				{
					m_Stopped.store(true);
					ServiceLocator::GetInstance().GetLogger()->Error(e.Message());
				}
				catch (const RuntimeException& e)
				{
					ServiceLocator::GetInstance().GetLogger()->Warning(e.Message());
				}
			}
		}

		void Receive()
		{
			while (!m_Stopped.load())
			{
				try {
					if (m_ClientReady && m_Transport.IsConnected())
					{
						const auto& message = m_Transport.Receive();
						ServiceLocator::GetInstance().GetLogger()->Info(L"received message from client: {}", message);
						std::lock_guard<std::recursive_mutex> guard(WorldStateMutex());
						const auto messageType = m_IncomingMessageProcessor.Process(message);

						if (messageType == Serializers::IncomingMessage::Type::invalidate) {
							Invalidate();
						}
					}
					std::this_thread::sleep_for(std::chrono::milliseconds(50));
				}
				catch (const CriticalRuntimeException& e)
				{
					m_Stopped.store(true);
					ServiceLocator::GetInstance().GetLogger()->Error(e.Message());
				}
				catch (const RuntimeException& e)
				{
					ServiceLocator::GetInstance().GetLogger()->Warning(e.Message());
				}
			}
		}

		void Connect()
		{
			while (!m_Stopped.load())
			{
				try {
					if (!m_Transport.IsConnected())
					{
						m_ClientReady = false;
                        m_SnapshotReady = false;
						if (m_Transport.Connect())
						{
							std::lock_guard<std::recursive_mutex> guard(WorldStateMutex());
							m_OutgoingMessageBuilder.Reset();
							m_ClientReady = true;
						}
					}
					std::this_thread::sleep_for(std::chrono::milliseconds(50));
				}
				catch (const CriticalRuntimeException& e)
				{
					m_Stopped.store(true);
					ServiceLocator::GetInstance().GetLogger()->Error(e.Message());
				}
				catch (const RuntimeException& e)
				{
					ServiceLocator::GetInstance().GetLogger()->Warning(e.Message());
				}
			}
		}

		const std::vector<std::wstring> GetOutgoingMessages()
		{
			std::lock_guard<std::recursive_mutex> guard(WorldStateMutex());
			std::vector<std::wstring> result;

			// The client needs its hero before creating view models for other entities.
			const auto append = [this, &result](const auto& entry) {
				const auto entities = entry.second.GetEntities();
				for (const auto& message : m_OutgoingMessageBuilder.Build(entry.first, entities))
					result.push_back(m_Serializer.Serialize(message));
			};
			const auto hero = m_Repositories.find(L"hero");
			if (hero != m_Repositories.end()) append(*hero);
			for (const auto& entry : m_Repositories)
				if (entry.first != L"hero") append(entry);

			return result;
		}

		void Invalidate()
		{
			// Keep event-driven skills/items/effects; only force a full snapshot.
			std::lock_guard<std::recursive_mutex> guard(WorldStateMutex());
			m_OutgoingMessageBuilder.Reset();
            m_SnapshotReady = true;
		}

	private:
		const std::map<std::wstring, Repositories::EntityRepositoryInterface&> m_Repositories;
		const Serializers::SerializerInterface& m_Serializer;
		const Services::IncomingMessageProcessor m_IncomingMessageProcessor;
		Services::OutgoingMessageBuilder m_OutgoingMessageBuilder;

		Transports::TransportInterface& m_Transport;
		std::atomic_bool m_Stopped = false;
		std::atomic_bool m_ClientReady = false;
        std::atomic_bool m_SnapshotReady = false;
		std::thread m_ConnectingThread;
		std::thread m_SendingThread;
		std::thread m_ReceivingThread;
	};

}
