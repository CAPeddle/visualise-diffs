#pragma once
#include "shape.h"

namespace synthetic {

class Disc : public Shape {
public:
    Disc(double r);
    double radius;
    double area() const;
    double perimeter() const;
    double diameter() const;
};

}  // namespace synthetic
