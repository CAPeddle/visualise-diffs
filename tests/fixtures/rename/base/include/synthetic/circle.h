#pragma once
#include "shape.h"

namespace synthetic {

class Circle : public Shape {
public:
    Circle(double r);
    double radius;
    double area() const;
    double perimeter() const;
};

}  // namespace synthetic
