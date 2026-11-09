#pragma once
#include "Domain/Services/WorldState.h"

#include <map>
#include <mutex>
#include <chrono>
#include "Domain/Events/AttackedEvent.h"
#include <shared_mutex>
#include "../GameStructs/NetworkHandlerWrapper.h"
#include "Domain/Repositories/EntityRepositoryInterface.h"
#include "../Factories/NPCFactory.h"
#include "Domain/Events/SpoiledEvent.h"
#include "Domain/Events/CreatureDiedEvent.h"
#include "../../GameStructs/FindObjectsTrait.h"
#include "Domain/Services/ServiceLocator.h"

using namespace L2Bot::Domain;

namespace Interlude
{
	class NPCRepository : public Repositories::EntityRepositoryInterface, public FindObjectsTrait
	{
	public:
		const std::unordered_map<std::uint32_t, std::shared_ptr<Entities::EntityInterface>> GetEntities() override
		{
			std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());

			const auto now = std::chrono::steady_clock::now();
			for (auto actor = m_Combat.begin(); actor != m_Combat.end(); )
			{
				for (auto opponent = actor->second.begin(); opponent != actor->second.end(); )
				{
					if (now - opponent->second >= std::chrono::seconds(15)) opponent = actor->second.erase(opponent);
					else ++opponent;
				}
				if (actor->second.empty()) actor = m_Combat.erase(actor);
				else ++actor;
			}
			const auto hero = m_NetworkHandler.GetHero();

			const auto allCreatures = FindAllObjects<User*>(m_Radius, [this](float_t radius, int32_t prevId) {
				return m_NetworkHandler.GetNextCreature(radius, prevId);
			});

			std::unordered_map<std::uint32_t, std::shared_ptr<Entities::EntityInterface>> result;
			for (const auto kvp : allCreatures) {
				const auto creature = kvp.second;
				if (creature->userType != L2::UserType::NPC) {
					continue;
				}

				if (m_Npcs.find(creature->objectId) == m_Npcs.end()) {
					m_Npcs[creature->objectId] = m_Factory.Create(creature);
				}
				else
				{
					m_Factory.Update(m_Npcs[creature->objectId], creature);
				}

				const auto spoilState = m_Spoiled.find(creature->objectId) == m_Spoiled.end() ? Enums::SpoilStateEnum::none : m_Spoiled[creature->objectId];
				m_Npcs[creature->objectId]->UpdateSpoilState(spoilState);

				bool fightingOther = false;
				const auto combat = m_Combat.find(creature->objectId);
				if (combat != m_Combat.end())
				{
					for (const auto& opponent : combat->second)
					{
						if (!hero || opponent.first != hero->objectId) fightingOther = true;
					}
				}
				m_Npcs[creature->objectId]->UpdateCombatWithOther(fightingOther);

				result[creature->objectId] = m_Npcs[creature->objectId];
			}

			return result;
		}

		void Reset() override
		{
			std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());
			m_Npcs.clear();
			m_Combat.clear();
			m_Spoiled.clear();
		}

		void Init() override
		{
			Services::ServiceLocator::GetInstance().GetEventDispatcher()->Subscribe(Events::AttackedEvent::name, [this](const Events::Event& evt) {
				const auto& attack = static_cast<const Events::AttackedEvent&>(evt);
				std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());
				if (!attack.GetAttackerId() || !attack.GetTargetId()) return;
				const auto now = std::chrono::steady_clock::now();
				m_Combat[attack.GetAttackerId()][attack.GetTargetId()] = now;
				m_Combat[attack.GetTargetId()][attack.GetAttackerId()] = now;
			});
			Services::ServiceLocator::GetInstance().GetEventDispatcher()->Subscribe(Events::SpoiledEvent::name, [this](const Events::Event& evt) {
				OnSpoiled(evt);
			});
			Services::ServiceLocator::GetInstance().GetEventDispatcher()->Subscribe(Events::CreatureDiedEvent::name, [this](const Events::Event& evt) {
				OnCreatureDied(evt);
			});
		}

		NPCRepository(const NetworkHandlerWrapper& networkHandler, const NPCFactory& factory, const uint16_t radius) :
			m_NetworkHandler(networkHandler),
			m_Factory(factory),
			m_Radius(radius)
		{
		}

		NPCRepository() = delete;
		virtual ~NPCRepository() = default;

		void OnSpoiled(const Events::Event& evt)
		{
			std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());
			if (evt.GetName() == Events::SpoiledEvent::name)
			{
				const auto casted = static_cast<const Events::SpoiledEvent&>(evt);
				const auto hero = m_NetworkHandler.GetHero();
				if (hero && hero->pawn && hero->pawn->lineagePlayerController)
				{
					const auto targetId = hero->pawn->lineagePlayerController->targetObjectId;
					if (targetId)
					{
						m_Spoiled[targetId] = Enums::SpoilStateEnum::spoiled;
					}
				}
			}
		}

		void OnCreatureDied(const Events::Event& evt)
		{
			std::lock_guard<std::recursive_mutex> guard(Services::WorldStateMutex());
			if (evt.GetName() == Events::CreatureDiedEvent::name)
			{
				const auto casted = static_cast<const Events::CreatureDiedEvent&>(evt);
				m_Combat.erase(casted.GetCreatureId());
				for (auto& actor : m_Combat) actor.second.erase(casted.GetCreatureId());
				if (m_Spoiled.find(casted.GetCreatureId()) != m_Spoiled.end())
				{
					const auto& info = casted.GetCreatureInfo();
					const auto isSweepable = info.size() > 4 && info[4] != 0;
					if (m_Spoiled[casted.GetCreatureId()] == Enums::SpoilStateEnum::spoiled && isSweepable)
					{
						m_Spoiled[casted.GetCreatureId()] = Enums::SpoilStateEnum::sweepable;
					}
					else
					{
						m_Spoiled[casted.GetCreatureId()] = Enums::SpoilStateEnum::none;
					}
				}
			}
		}

	private:
		std::map<uint32_t, std::map<uint32_t, std::chrono::steady_clock::time_point>> m_Combat;
		const NPCFactory& m_Factory;
		std::map<uint32_t, Enums::SpoilStateEnum> m_Spoiled;
		const NetworkHandlerWrapper& m_NetworkHandler;
		const uint16_t m_Radius = 0;
		std::unordered_map<uint32_t, std::shared_ptr<Entities::NPC>> m_Npcs;
	};
}
