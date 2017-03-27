#include "systemstate.h"


uint32_t SystemState::inputQueue_countPush;
std::mutex SystemState::mutex_inputQueue_countPush;

uint32_t SystemState::inputQueue_countPop;
std::mutex SystemState::mutex_inputQueue_countPop;

uint32_t SystemState::outputQueue_countPush;
std::mutex SystemState::mutex_outputQueue_countPush;

uint32_t SystemState::outputQueue_countPop;
std::mutex SystemState::mutex_outputQueue_countPop;
