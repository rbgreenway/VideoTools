#ifndef SAMPLEFILESTORAGE_H
#define SAMPLEFILESTORAGE_H


#include "bento4\include\Ap4.h"


/*----------------------------------------------------------------------
|   constants
+---------------------------------------------------------------------*/

const unsigned int AP4_MUX_DEFAULT_VIDEO_FRAME_RATE = 24;

/*----------------------------------------------------------------------
|   globals
+---------------------------------------------------------------------*/
static struct {
    bool verbose;
} Options;

/*----------------------------------------------------------------------
|   SampleOrder
+---------------------------------------------------------------------*/
struct SampleOrder {
    SampleOrder(AP4_UI32 decode_order, AP4_UI32 display_order) :
        m_DecodeOrder(decode_order),
        m_DisplayOrder(display_order) {}
    AP4_UI32 m_DecodeOrder;
    AP4_UI32 m_DisplayOrder;
};

/*----------------------------------------------------------------------
|   Parameter
+---------------------------------------------------------------------*/
struct Parameter {
    Parameter(const char* name, const char* value) :
        m_Name(name),
        m_Value(value) {}
    AP4_String m_Name;
    AP4_String m_Value;
};




/*----------------------------------------------------------------------
|   SampleFileStorage
+---------------------------------------------------------------------*/
class SampleFileStorage
{
public:
    SampleFileStorage(const char* basename);
    ~SampleFileStorage();

    void            SortSamples(SampleOrder* array, unsigned int n);
    AP4_Result      StoreSample(AP4_Sample& from_sample, AP4_Sample& to_sample);
    AP4_ByteStream* GetStream();

    AP4_ByteStream* m_Stream;
    AP4_String      m_Filename;
};


#endif // SAMPLEFILESTORAGE_H
