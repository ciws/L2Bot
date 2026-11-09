#pragma once
#include <cstdint>

#include "Domain/Serializers/SerializerInterface.h"
#include "Domain/Serializers/Node.h"

using namespace L2Bot::Domain;

class JsonSerializer : public Serializers::SerializerInterface
{
public:
	const std::wstring Serialize(std::vector<Serializers::Node> nodes, const bool isArray = false) const override
	{
		std::wstring result = isArray ? L"[" : L"{";

		for (auto it = nodes.begin(); it != nodes.end(); ++it)
		{
			if (!isArray)
			{
				result += Quote(it->name) + L":";
			}
			if (it->isContainer)
			{
				result += Serialize(it->children, it->isArray);
			}
			else
			{
				result += Quote(it->value);
			}
			if (std::next(it) != nodes.end())
			{
				result += L",";
			}
		}

		result += isArray ? L"]" : L"}";

		return result;
	}
private:
	static std::wstring Quote(const std::wstring& value)
	{
		return L"\"" + Escape(value) + L"\"";
	}

	static std::wstring Escape(const std::wstring& value)
	{
		std::wstring result;
		const wchar_t hex[] = L"0123456789abcdef";
		for (const auto character : value)
		{
			if (character == L'"' || character == L'\\')
			{
				result += L'\\';
				result += character;
			}
			else if (static_cast<uint32_t>(character) < 0x20)
			{
				result += L"\\u00";
				result += hex[(character >> 4) & 15];
				result += hex[character & 15];
			}
			else result += character;
		}
		return result;
	}
};
