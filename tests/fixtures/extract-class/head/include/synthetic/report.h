#pragma once
#include "formatter.h"

namespace synthetic {

class Report {
public:
    void print() { formatter.write(); }
    Formatter formatter;
};

}  // namespace synthetic
