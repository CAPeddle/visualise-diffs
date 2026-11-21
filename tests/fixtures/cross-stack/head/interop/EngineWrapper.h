#pragma once

#include <vcclr.h>

namespace Synthetic {
namespace Interop {

public ref class EngineWrapper {
public:
    int Speed();
    void Boost(int amount);
};

class EngineEvents {
public:
    gcroot<System::Object^> payload;
};

}  // namespace Interop
}  // namespace Synthetic
