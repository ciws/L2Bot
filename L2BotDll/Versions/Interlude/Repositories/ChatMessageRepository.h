#pragma once
#include "Domain/Services/WorldState.h"

#include <vector>
#include <algorithm>
#include <shared_mutex>
#include "../Factories/ChatMessageFactory.h"
#include "Domain/Events/ChatMessageCreatedEvent.h"
#include "Domain/Services/ServiceLocator.h"

using namespace L2Bot::Domain;

namespace Interlude
{
	class ChatMessageRepository : public Repositories::EntityRepositoryInterface
	{
	public:
		const std::unordered_map<std::uint32_t, std::shared_ptr<Entities::EntityInterface>> GetEntities() override
		{
			std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());

			return m_Messages;
		}

		void Reset() override
		{
			std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());
			m_Messages.clear();
		}

		void Init() override
		{
			Services::ServiceLocator::GetInstance().GetEventDispatcher()->Subscribe(Events::ChatMessageCreatedEvent::name, [this](const Events::Event& evt) {
				OnMessageCreated(evt);
			});
		}

		ChatMessageRepository(const ChatMessageFactory& factory) :
			m_Factory(factory)
		{
		}

		void OnMessageCreated(const Events::Event& evt)
		{
			std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());
			if (evt.GetName() == Events::ChatMessageCreatedEvent::name)
			{
				const auto casted = static_cast<const Events::ChatMessageCreatedEvent&>(evt);

				const auto message = m_Factory.Create(casted.GetChatMessage());
				m_Messages[message->GetId()] = message;
				if (m_Messages.size() > 500)
				{
					const auto oldest = std::min_element(m_Messages.begin(), m_Messages.end(),
						[](const auto& a, const auto& b) { return a.first < b.first; });
					m_Messages.erase(oldest);
				}
			}
		}

		ChatMessageRepository() = delete;
		virtual ~ChatMessageRepository() = default;

	private:
		const ChatMessageFactory& m_Factory;
		std::unordered_map<uint32_t, std::shared_ptr<Entities::EntityInterface>> m_Messages;
	};
}
