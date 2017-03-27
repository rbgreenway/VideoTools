#ifndef AVIWRITER_H
#define AVIWRITER_H


#include <Windows.h>
#include <Vfw.h>

#include <algorithm>
#include <string>
#include <stdint.h>


class AviWriter
{
public:
    AviWriter(std::string outputFilename, int width, int height, int framerate);
    ~AviWriter();

    PAVIFILE			m_AviFile;
    PAVISTREAM			m_AviStream;
    AVISTREAMINFO		m_AviStreamInfo;

    std::string         m_outputFilename;

    HRESULT AddFrame(char *frameBuffer, uint32_t numBytes, DWORD dwFlags, LONG frameNumber);

};

#endif // AVIWRITER_H
