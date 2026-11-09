#pragma once
#include <mutex>

namespace L2Bot::Domain::Services
{
    // Reentrant because repository updates may synchronously dispatch other repository events.
    // Protects repository state and serialization together, not just the container of pointers.
    inline std::recursive_mutex& WorldStateMutex()
    {
        static std::recursive_mutex mutex;
        return mutex;
    }
}
