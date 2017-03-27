#ifndef SYSTEMSTATE_H
#define SYSTEMSTATE_H

#include <stdint.h>
#include <mutex>

class SystemState
{

private:

    static uint32_t inputQueue_countPush;
    static std::mutex mutex_inputQueue_countPush;

    static uint32_t inputQueue_countPop;
    static std::mutex mutex_inputQueue_countPop;

    static uint32_t outputQueue_countPush;
    static std::mutex mutex_outputQueue_countPush;

    static uint32_t outputQueue_countPop;
    static std::mutex mutex_outputQueue_countPop;

public:

    static uint32_t SS_get_inputQueue_countPush()
    {
        uint32_t count = 0;
        mutex_inputQueue_countPush.lock();
            count = inputQueue_countPush;
        mutex_inputQueue_countPush.unlock();
        return count;
    }

    static void SS_set_inputQueue_countPush(uint32_t count)
    {
        mutex_inputQueue_countPush.lock();
            inputQueue_countPush = count;
        mutex_inputQueue_countPush.unlock();
    }

    static void SS_increment_inputQueue_countPush()
    {
        mutex_inputQueue_countPush.lock();
            inputQueue_countPush++;
        mutex_inputQueue_countPush.unlock();
    }






    static uint32_t SS_get_inputQueue_countPop()
    {
        uint32_t count = 0;
        mutex_inputQueue_countPop.lock();
            count = inputQueue_countPop;
        mutex_inputQueue_countPop.unlock();
        return count;
    }

    static void SS_set_inputQueue_countPop(uint32_t count)
    {
        mutex_inputQueue_countPop.lock();
            inputQueue_countPop = count;
        mutex_inputQueue_countPop.unlock();
    }

    static void SS_increment_inputQueue_countPop()
    {
        mutex_inputQueue_countPop.lock();
            inputQueue_countPop++;
        mutex_inputQueue_countPop.unlock();
    }






    static uint32_t SS_get_outputQueue_countPush()
    {
        uint32_t count = 0;
        mutex_outputQueue_countPush.lock();
            count = outputQueue_countPush;
        mutex_outputQueue_countPush.unlock();
        return count;
    }

    static void SS_set_outputQueue_countPush(uint32_t count)
    {
        mutex_outputQueue_countPush.lock();
            outputQueue_countPush = count;
        mutex_outputQueue_countPush.unlock();
    }

    static void SS_increment_outputQueue_countPush()
    {
        mutex_outputQueue_countPush.lock();
            outputQueue_countPush++;
        mutex_outputQueue_countPush.unlock();
    }






    static uint32_t SS_get_outputQueue_countPop()
    {
        uint32_t count = 0;
        mutex_outputQueue_countPop.lock();
            count = outputQueue_countPop;
        mutex_outputQueue_countPop.unlock();
        return count;
    }

    static void SS_set_outputQueue_countPop(uint32_t count)
    {
        mutex_outputQueue_countPop.lock();
            outputQueue_countPop = count;
        mutex_outputQueue_countPop.unlock();
    }

    static void SS_increment_outputQueue_countPop()
    {
        mutex_outputQueue_countPop.lock();
            outputQueue_countPop++;
        mutex_outputQueue_countPop.unlock();
    }
};

#endif // SYSTEMSTATE_H
