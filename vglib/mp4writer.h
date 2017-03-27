#ifndef MP4WRITER_H
#define MP4WRITER_H

#include <stdint.h>
#include <string>
#include <algorithm>


#include "bento4\include\Ap4.h"

#include "samplefilestorage.h"

enum MP4_ENCODER_TYPE
{
    H264,
    JPEG
};

class Mp4Writer
{
public:
    Mp4Writer(std::string outputFilename, float defaultFramesPerSecond);
    ~Mp4Writer();

    AP4_Movie *                 mp_movie;
    AP4_Array<AP4_UI32>         m_brands;
    SampleFileStorage *         mp_sample_storage;

    AP4_SyntheticSampleTable*   mp_sample_table;
    AP4_Array<SampleOrder>      m_sample_orders;
    AP4_AvcFrameParser          m_parser;

    std::string                 m_outputFilename;
    unsigned int                m_frameRate;

    AP4_String                  m_errorMsg;


    int8_t AddFrame(char* frameBuffer, uint32_t numBytes);
    int8_t WriteFile(int frameRate);
    AP4_String GetLastErrorMessage();
};

#endif // MP4WRITER_H
