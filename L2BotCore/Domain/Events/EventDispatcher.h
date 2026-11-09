#pragma once

#include <string>
#include <functional>
#include <unordered_map>
#include <vector>
#include <mutex>
#include "Event.h"

namespace L2Bot::Domain::Events
{
	class EventDispatcher
	{
	public:
		using Delegate = std::function<void(const Event&)>;

		EventDispatcher() = default;
		virtual ~EventDispatcher() = default;

		void Dispatch(const Event& evt)
		{
			std::vector<Delegate> handlers;
			{
				std::lock_guard<std::mutex> guard(m_Mutex);
				const auto found = m_Handlers.find(evt.GetName());
				if (found != m_Handlers.end()) handlers = found->second;
			}
			for (const auto& handler : handlers) handler(evt);
		}

		void Subscribe(const std::string& eventName, const Delegate handler)
		{
			std::lock_guard<std::mutex> guard(m_Mutex);
			m_Handlers[eventName].push_back(handler);
		}

	private:
		std::unordered_map<std::string, std::vector<Delegate>> m_Handlers;
		std::mutex m_Mutex;
	};
}
