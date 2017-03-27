#include "samplefilestorage.h"


SampleFileStorage::SampleFileStorage(const char *basename) {
        m_Stream = NULL;
        AP4_Size name_length = (AP4_Size)AP4_StringLength(basename);
        char* filename = new char[name_length+2];
        AP4_CopyMemory(filename, basename, name_length);
        filename[name_length]   = '_';
        filename[name_length+1] = '\0';
        m_Filename = filename;
        delete[] filename;
}




SampleFileStorage::~SampleFileStorage()
{
    m_Stream->Release();
    remove(m_Filename.GetChars());
}




void SampleFileStorage::SortSamples(SampleOrder* array, unsigned int n)
{
    if (n < 2) {
        return;
    }
    SampleOrder pivot = array[n / 2];
    SampleOrder* left  = array;
    SampleOrder* right = array + n - 1;
    while (left <= right) {
        if (left->m_DisplayOrder < pivot.m_DisplayOrder) {
            ++left;
            continue;
        }
        if (right->m_DisplayOrder > pivot.m_DisplayOrder) {
            --right;
            continue;
        }
        SampleOrder temp = *left;
        *left++ = *right;
        *right-- = temp;
    }
    SortSamples(array, (unsigned int)(right - array + 1));
    SortSamples(left, (unsigned int)(array + n - left));
}




AP4_Result SampleFileStorage::StoreSample(AP4_Sample& from_sample, AP4_Sample& to_sample) {
    // clone the sample fields
    to_sample = from_sample;

    // read the sample data
    AP4_DataBuffer sample_data;
    AP4_Result result = from_sample.ReadData(sample_data);
    if (AP4_FAILED(result)) return result;

    // mark where we are going to store the sample data
    AP4_Position position;
    m_Stream->Tell(position);
    to_sample.SetOffset(position);

    // write the sample data
    result = m_Stream->Write(sample_data.GetData(), sample_data.GetDataSize());
    if (AP4_FAILED(result)) return result;

    // update the stream for the new sample
    to_sample.SetDataStream(*m_Stream);

    return AP4_SUCCESS;
}

AP4_ByteStream *SampleFileStorage::GetStream()
{
    return m_Stream;
}
