#pragma once
#include "Domain/Services/WorldState.h"

#include <unordered_map>
#include <chrono>
#include <algorithm>
#include <map>
#include <shared_mutex>
#include "Domain/Repositories/EntityRepositoryInterface.h"
#include "../Factories/SkillFactory.h"
#include "Domain/Events/SkillCreatedEvent.h"
#include "Domain/Events/SkillUsedEvent.h"
#include "Domain/Events/SkillCancelledEvent.h"
#include "Domain/Events/AbnormalEffectChangedEvent.h"
#include "Domain/Events/HeroDeletedEvent.h"
#include "Domain/Events/GameEngineTickedEvent.h"
#include "../GameStructs/NetworkHandlerWrapper.h"
#include "Domain/Services/ServiceLocator.h"

using namespace L2Bot::Domain;

namespace Interlude
{
	class SkillRepository : public Repositories::EntityRepositoryInterface
	{
	public:
		const std::unordered_map<std::uint32_t, std::shared_ptr<Entities::EntityInterface>> GetEntities() override
		{
			std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());

			const auto now = std::chrono::steady_clock::now();
			ExpireTimers(m_CastingUntil, now, false);
			ExpireTimers(m_ReloadingUntil, now, true);
			std::unordered_map<std::uint32_t, std::shared_ptr<Entities::EntityInterface>> result(m_Skills.begin(), m_Skills.end());
			return result;
		}

		SkillRepository(const NetworkHandlerWrapper& networkHandler, const SkillFactory& factory) :
			m_NetworkHandler(networkHandler),
			m_Factory(factory)
		{
			
		}

		SkillRepository() = delete;
		virtual ~SkillRepository() = default;

		void Reset() override
		{
			std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());
			m_CastingUntil.clear();
			m_ReloadingUntil.clear();
			m_Skills.clear();
			m_IsNewCycle = false;
			m_NewSkills.clear();
		}

		void Init() override
		{
			Services::ServiceLocator::GetInstance().GetEventDispatcher()->Subscribe(Events::SkillCreatedEvent::name, [this](const Events::Event& evt) {
				OnSkillCreated(evt);
			});
			Services::ServiceLocator::GetInstance().GetEventDispatcher()->Subscribe(Events::SkillUsedEvent::name, [this](const Events::Event& evt) {
				OnSkillUsed(evt);
			});
			Services::ServiceLocator::GetInstance().GetEventDispatcher()->Subscribe(Events::SkillCancelledEvent::name, [this](const Events::Event& evt) {
				OnSkillCancelled(evt);
			});
			Services::ServiceLocator::GetInstance().GetEventDispatcher()->Subscribe(Events::AbnormalEffectChangedEvent::name, [this](const Events::Event& evt) {
				OnSkillToggled(evt);
			});
			Services::ServiceLocator::GetInstance().GetEventDispatcher()->Subscribe(Events::HeroDeletedEvent::name, [this](const Events::Event& evt) {
				OnHeroDeleted(evt);
			});
			Services::ServiceLocator::GetInstance().GetEventDispatcher()->Subscribe(Events::GameEngineTickedEvent::name, [this](const Events::Event& evt) {
				OnGameEngineTicked(evt);
			});
		}

		void OnGameEngineTicked(const Events::Event& evt)
		{
			std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());
			if (evt.GetName() == Events::GameEngineTickedEvent::name)
			{
				for (auto it = m_Skills.begin(); it != m_Skills.end();)
				{
					if (m_NewSkills.find(it->first) == m_NewSkills.end())
					{
						it = m_Skills.erase(it);
					}
					else
					{
						++it;
					}
				}

				m_IsNewCycle = true;
			}
		}

		void OnHeroDeleted(const Events::Event& evt)
		{
			std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());
			if (evt.GetName() == Events::HeroDeletedEvent::name)
			{
				Reset();
			}
		}

		//todo need to delete skills if they are not exists in create "queue"
		void OnSkillCreated(const Events::Event& evt)
		{
			std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());
			if (evt.GetName() == Events::SkillCreatedEvent::name)
			{
				if (m_IsNewCycle)
				{
					m_IsNewCycle = false;
					m_NewSkills.clear();
				}

				const auto casted = static_cast<const Events::SkillCreatedEvent&>(evt);
				const auto skillInfo = casted.GetSkillInfo();
				if (skillInfo.size() < 3) return;
				const auto skillId = skillInfo[2];

				if (m_Skills.find(skillId) == m_Skills.end())
				{
					const auto newSkill = m_Factory.Create(
						skillInfo[2],
						skillInfo[1],
						skillInfo[0]
					);
					if (newSkill) {
						m_Skills[skillId] = newSkill;
					}
				}
				else
				{
					auto skill = m_Skills[skillId];
					m_Factory.Update(
						skill,
						skillInfo[2],
						skillInfo[1],
						skillInfo[0]
					);
				}
				m_NewSkills[skillId] = skillId;
			}
		}
		void OnSkillUsed(const Events::Event& evt)
		{
			std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());
			if (evt.GetName() == Events::SkillUsedEvent::name)
			{
				const auto casted = static_cast<const Events::SkillUsedEvent&>(evt);
				const auto hero = m_NetworkHandler.GetHero();
				if (!hero || casted.GetCasterId() != hero->objectId) return;
				const auto skillInfo = casted.GetSkillInfo();
				if (skillInfo.size() < 4) return;
				const auto skillId = skillInfo[0];

				if (m_Skills.find(skillId) == m_Skills.end())
				{
					//todo exception?
					return;
				}

				const auto& skill = m_Skills[skillId];

				skill->StartReloading();
				skill->StartCasting();

				m_UsedSkillId = skill->GetId();

				const auto now = std::chrono::steady_clock::now();
				m_ReloadingUntil[skillId] = now + std::chrono::milliseconds((std::max)(0, skillInfo[3]));
				m_CastingUntil[skillId] = now + std::chrono::milliseconds((std::max)(0, skillInfo[2]));
			}
		}
		void OnSkillCancelled(const Events::Event& evt)
		{
			std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());
			if (evt.GetName() == Events::SkillCancelledEvent::name)
			{
				const auto casted = static_cast<const Events::SkillCancelledEvent&>(evt);

				const auto hero = m_NetworkHandler.GetHero();

				if (hero && hero->objectId == casted.GetInitiatorId())
				{
					if (m_Skills.find(m_UsedSkillId) == m_Skills.end())
					{
						//todo exception?
						return;
					}

					m_Skills[m_UsedSkillId]->StopCasting();
					m_CastingUntil.erase(m_UsedSkillId);

					m_UsedSkillId = 0;
				}
			}
		}
		void OnSkillToggled(const Events::Event& evt)
		{
			std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());
			if (evt.GetName() == Events::AbnormalEffectChangedEvent::name)
			{
				const auto casted = static_cast<const Events::AbnormalEffectChangedEvent&>(evt);
				const auto skillInfo = casted.GetSkillInfo();

				std::unordered_map<uint32_t, int32_t> ids;

				for (size_t i = 0; i + 2 < skillInfo.size(); i += 3)
				{
					ids[skillInfo[i]] = skillInfo[i + 2];
				}

				for (const auto& kvp : m_Skills)
				{
					const auto& skill = kvp.second;

					const auto needToToggle = ids.find(skill->GetId()) != ids.end();
					// buff time less than zero means this is a aura
					const auto isAura = needToToggle ? ids[skill->GetId()] < 0 : false;

					if (skill->IsToggled() && !needToToggle)
					{
						skill->ToggleOff();
					}
					else if (!skill->IsToggled() && needToToggle && isAura)
					{
						skill->ToggleOn();
					}
				}
			}
		}

	private:
		const SkillFactory& m_Factory;
		std::unordered_map<std::uint32_t, std::shared_ptr<Entities::Skill>> m_Skills;
		std::unordered_map<std::uint32_t, std::uint32_t> m_NewSkills;
		bool m_IsNewCycle = true;
		uint32_t m_UsedSkillId = 0;
		const NetworkHandlerWrapper& m_NetworkHandler;
		using Deadlines = std::map<uint32_t, std::chrono::steady_clock::time_point>;
		Deadlines m_ReloadingUntil;
		Deadlines m_CastingUntil;

		void ExpireTimers(Deadlines& timers, std::chrono::steady_clock::time_point now, bool reload)
		{
			for (auto it = timers.begin(); it != timers.end(); )
			{
				const auto skill = m_Skills.find(it->first);
				if (skill == m_Skills.end()) it = timers.erase(it);
				else if (now >= it->second)
				{
					if (reload) skill->second->StopReloading();
					else skill->second->StopCasting();
					it = timers.erase(it);
				}
				else ++it;
			}
		}
	};
}
