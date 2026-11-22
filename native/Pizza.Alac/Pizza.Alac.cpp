#include <cstdint>
#include <cstring>
#include <new>
#include <memory>
#include "ALACEncoder.h"

class Encoder final : public ALACEncoder
{
public:
    AudioFormatDescription input{};
    AudioFormatDescription output{};
    uint32_t capacity() const { return mMaxOutputBytes; }
};

extern "C" __declspec(dllexport) void* pizza_alac_create(uint32_t rate, uint32_t channels, uint32_t bits, uint32_t* capacity)
{
    if (!capacity || rate < 8000 || rate > 384000 || channels < 1 || channels > 8 || (bits != 16 && bits != 24)) return nullptr;
    try
    {
        auto encoder = std::make_unique<Encoder>();
        encoder->input.mSampleRate = rate;
        encoder->input.mFormatID = kALACFormatLinearPCM;
        encoder->input.mFormatFlags = kALACFormatFlagIsSignedInteger | kALACFormatFlagIsPacked;
        encoder->input.mFramesPerPacket = 1;
        encoder->input.mChannelsPerFrame = channels;
        encoder->input.mBitsPerChannel = bits;
        encoder->input.mBytesPerFrame = channels * (bits / 8);
        encoder->input.mBytesPerPacket = encoder->input.mBytesPerFrame;
        encoder->output.mSampleRate = rate;
        encoder->output.mFormatID = kALACFormatAppleLossless;
        encoder->output.mFormatFlags = bits == 16 ? 1 : 3;
        encoder->output.mFramesPerPacket = 4096;
        encoder->output.mChannelsPerFrame = channels;
        if (encoder->InitializeEncoder(encoder->output) != 0)
        {
            return nullptr;
        }
        *capacity = encoder->capacity();
        return encoder.release();
    }
    catch (...) { return nullptr; }
}

extern "C" __declspec(dllexport) int32_t pizza_alac_encode(void* state, const uint8_t* pcm, int32_t bytes, uint8_t* output, uint32_t capacity)
{
    auto encoder = static_cast<Encoder*>(state);
    if (!encoder || !pcm || !output || bytes <= 0 || capacity < encoder->capacity()) return -1;
    const auto frameBytes = encoder->input.mBytesPerFrame;
    if (bytes % frameBytes != 0 || bytes / frameBytes > 4096) return -1;
    try
    {
        int32_t size = bytes;
        if (encoder->Encode(encoder->input, encoder->output, const_cast<uint8_t*>(pcm), output, &size) != 0
            || size <= 0 || static_cast<uint32_t>(size) > capacity) return -1;
        return size;
    }
    catch (...) { return -1; }
}

extern "C" __declspec(dllexport) int32_t pizza_alac_config(void* state, uint8_t* output, uint32_t capacity)
{
    static_assert(sizeof(ALACSpecificConfig) == 24);
    auto encoder = static_cast<Encoder*>(state);
    if (!encoder || !output || capacity < sizeof(ALACSpecificConfig)) return -1;
    if (encoder->Finish() != 0) return -1;
    ALACSpecificConfig config{};
    encoder->GetConfig(config);
    std::memcpy(output, &config, sizeof(config));
    return sizeof(config);
}

extern "C" __declspec(dllexport) void pizza_alac_destroy(void* state)
{
    delete static_cast<Encoder*>(state);
}
