#include <algorithm>
#include <iostream>
#include <fstream>
#include <sstream>
#include <memory>
#include <thread>
#include <mutex>
#include <atomic>
#include "L2BotDll/Serializers/JsonSerializer.h"
#include "L2BotDll/Serializers/JsonIncomingMessageFactory.h"
#include "L2BotCore/Domain/Services/OutgoingMessageBuilder.h"
#include "L2BotCore/Domain/Entities/EtcItem.h"
#include "L2BotCore/Domain/Services/IncomingMessageProcessor.h"
#include "L2BotCore/Domain/Services/WorldState.h"
#ifdef _WIN32
#include "L2BotDll/Transports/NamedPipeTransport.h"
#include "L2BotCore/Domain/Services/WorldHandler.h"
std::wstring GenerateUUID() { return L"L2Bot-test-main-" + std::to_wstring(GetCurrentProcessId()) + L"-" + std::to_wstring(GetTickCount64()); }
#endif
using namespace L2Bot::Domain;
void check(bool ok, const char* message) { if (!ok) throw std::runtime_error(message); }
class Commands : public Services::HeroServiceInterface {
public:
 mutable std::vector<std::string> seen;
 mutable std::atomic_bool done=false;
 void Move(ValueObjects::Vector3 p) const override { check(p.GetX()==10 && p.GetY()==-20 && p.GetZ()==30,"Move coordinates"); seen.push_back("Move"); }
 void AcquireTarget(int id) const override { check(id==42,"Target ID"); seen.push_back("AcquireTarget"); }
 void Attack(int id) const override { check(id==42,"Attack ID"); seen.push_back("Attack"); }
 void Pickup(int id) const override { check(id==42,"Pickup ID"); seen.push_back("Pickup"); }
 void UseSkill(int id,bool force,bool shift) const override { check(id==42 && !force && !shift,"Skill command"); seen.push_back("UseSkill"); }
 void UseItem(int id) const override { check(id==42,"Item ID"); seen.push_back("UseItem"); }
 void ToggleAutouseSoulshot(int id) const override { check(id==42,"Shot ID"); seen.push_back("ToggleSoulshot"); }
 void Sit() const override { seen.push_back("Sit"); }
 void Stand() const override { seen.push_back("Stand"); done=true; }
};
int main(int argc,char** argv) {
 try {
  JsonSerializer serializer; JsonIncomingMessageFactory parser;
#ifdef _WIN32
  if (argc==3 && std::string(argv[1])=="--pipe-server") {
   Services::ServiceLocator::GetInstance().SetLogger(std::make_unique<Logger::Logger>(std::vector<std::unique_ptr<Logger::LogChannel>>{}));
   class Repository : public Repositories::EntityRepositoryInterface {
    std::shared_ptr<Entities::EtcItem> item=std::make_shared<Entities::EtcItem>();
   public:
    const std::unordered_map<uint32_t,std::shared_ptr<Entities::EntityInterface>> GetEntities() override { return {{0,item}}; }
    void Reset() override { throw std::runtime_error("Resync must preserve event-driven repositories"); }
   } repository;
   std::string name(argv[2]); NamedPipeTransport transport(std::wstring(name.begin(),name.end()));
   Commands commands;
   Services::WorldHandler world({{L"item",repository}},serializer,Services::IncomingMessageProcessor(parser,commands),transport);
   world.Start();
   const auto deadline=std::chrono::steady_clock::now()+std::chrono::seconds(30);
   while(!commands.done && std::chrono::steady_clock::now()<deadline) std::this_thread::sleep_for(std::chrono::milliseconds(20));
   world.Stop(); check(commands.done,"Pipe test server timed out"); return 0;
  }
#endif
  if (argc==3 && std::string(argv[1])=="--commands") {
   Commands commands; Services::IncomingMessageProcessor processor(parser,commands);
   std::ifstream input(argv[2]); check(bool(input),"Missing C# command fixture");
   std::string line; int invalidate=0;
   while (std::getline(input,line)) {
    auto type=processor.Process(std::wstring(line.begin(),line.end()));
    check(type!=Serializers::IncomingMessage::Type::none,"C# command rejected by native parser");
    if(type==Serializers::IncomingMessage::Type::invalidate) invalidate++;
   }
   check(commands.seen.size()==9 && invalidate==1,"All ten commands must dispatch");
   std::cout<<"PASS real C# JSON -> native parser -> command service\n"; return 0;
  }
  const auto encoded=serializer.Serialize({{L"key\"\\",L"say \"hello\"\n\t\\\r\x01 Привет"}});
  auto json=nlohmann::json::parse(encoded);
  check(json.contains("key\"\\"),"Escape field names");
  check(json.begin().value().get<std::string>().find("say \"hello\"\n") == 0,"Escape text");
  Entities::EtcItem item; auto before=item.GetHash(); item.StartAutouse();
  check(item.GetHash()!=before,"Autouse participates in hash"); item.StopAutouse();
  check(!item.IsAutoused() && item.GetHash()==before,"Autouse can turn off");
  Services::OutgoingMessageBuilder builder;
  auto entity=std::make_shared<Entities::EtcItem>();
  std::unordered_map<uint32_t,std::shared_ptr<Entities::EntityInterface>> entities{{0,entity}};
  check(builder.Build(L"item",entities).size()==1,"Initial create");
  check(builder.Build(L"item",entities).empty(),"Unchanged state emits no delta");
  entity->StartAutouse(); check(builder.Build(L"item",entities).size()==1,"Autouse update emitted");
  builder.Reset(); auto replay=builder.Build(L"item",entities);
  check(replay.size()==1 && replay[0][1].value==L"create","Reconnect replays create, preserving entities");
  check(builder.Build(L"item",{}).size()==1,"Deletion emitted");
  int count=0; std::vector<std::thread> workers;
  for(int t=0;t<4;t++) workers.emplace_back([&]{for(int n=0;n<1000;n++) {
   std::lock_guard<std::recursive_mutex> a(Services::WorldStateMutex());
   std::lock_guard<std::recursive_mutex> b(Services::WorldStateMutex()); ++count;
  }});
  for(auto& worker:workers) worker.join(); check(count==4000,"Reentrant world state lock");
  check(parser.CreateMessage(L"{\"Type\":\"Invalidate\",\"Content\":0}").GetType()==Serializers::IncomingMessage::Type::invalidate,"Invalidate JSON");
  std::cout<<"PASS native escaping, autouse, snapshot replay, deletion, synchronization\n";
 } catch(const std::exception& e) { std::cerr<<e.what()<<'\n'; return 1; }
}
