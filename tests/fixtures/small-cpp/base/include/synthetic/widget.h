#pragma once

namespace synthetic {

class SYNTH_API Component {
public:
    int id() const { return id_; }

private:
    int id_ = 0;
};

class SYNTH_API Widget : public Component {
public:
    int width() const { return 1; }
    void paint();

protected:
    int height_ = 0;
};

}  // namespace synthetic
