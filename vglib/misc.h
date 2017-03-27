#ifndef MISC_H
#define MISC_H



#ifndef MIN
#  define MIN(a,b)  ((a) > (b) ? (b) : (a))
#endif

#pragma pack(push,1)
typedef struct _DATA_FRAME_HEADER
{
   uint8_t  StartCode[2];
   uint32_t TotalLength;
   uint32_t CameraID;
   uint8_t  StreamingMode;
   uint8_t  KeyFlag;
   uint16_t FrameWidth;
   uint16_t FrameHeight;
   uint16_t MotionTime;
} DATA_FRAME_HEADER;
#pragma pack(pop)


union RECV_STRUCT{
    _DATA_FRAME_HEADER header;
    char buffer[sizeof(_DATA_FRAME_HEADER)];
};


typedef enum VI_STREAMING_MODE
{
    StreamingMode_FAVOR_CPU_SPEED = 0,
    StreamingMode_FOR_NETWORK_BANDWIDTH = 1,
    StreamingMode_MJPG = 3,
    StreamingMode_VIP4 = 4,
    StreamingMode_WMV = 5,
    StreamingMode_DIVX = 6,
    StreamingMode_H264 = 8,
    StreamingMode_H263 = 9,
    StreamingMode_Audio = 10,
    StreamingMode_AVI = 11,
    StreamingMode_MXP = 13,
    StreamingMode_WMVSYNC = 21,
    StreamingMode_Automatic = 255
} VI_STREAMING_MODE;



class IMAGE_DATA
{
public:
    IMAGE_DATA()
    {
        pData = nullptr;
        stop = false;
    }
    ~IMAGE_DATA()
    {
        delete pData;
    }

    cudaVideoCodec_enum codec;
    uint32_t VIcameraID;
    std::mutex    mutex;
    unsigned char * pData;
    uint32_t   dataLength;
    unsigned width;
    unsigned height;
    bool keyFrame;
    bool decoded;
    bool stop;
};



#endif // MISC_H
