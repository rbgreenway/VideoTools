#include "aviwriter.h"

AviWriter::AviWriter(std::string outputFilename, int width, int height, int framerate)
{
    // make sure outputFilename is not empty
    if(outputFilename.empty()) outputFilename = "output.avi";


    // convert to lower case
    std::transform(outputFilename.begin(), outputFilename.end(), outputFilename.begin(), ::tolower);

    // make sure filename contains ".avi"
    std::size_t found = outputFilename.find(".avi");
    if (found==std::string::npos)
    {
        m_outputFilename = outputFilename + ".avi";
    }
    else
    {
        m_outputFilename = outputFilename;
    }


        // build filename in wide string format
        std::wstring widefilename;
        for(int i = 0; i < m_outputFilename.length(); ++i)
          widefilename += wchar_t( m_outputFilename[i] );


        HRESULT hr;
        AVIFileInit();

        if(FAILED(hr=AVIFileOpen(&m_AviFile, widefilename.c_str(), OF_CREATE|OF_WRITE, NULL)))
        {
            std::string errStr;
            switch(hr){
            case AVIERR_BADFORMAT:
                errStr = "The file couldn't be read, indicating a corrupt file or an unrecognized format.";
                break;
            case AVIERR_MEMORY:
                errStr = "The file could not be opened because of insufficient memory.";
                break;
            case AVIERR_FILEREAD:
                errStr = "A disk error occurred while reading the file.";
                break;
            case AVIERR_FILEOPEN:
                errStr = "A disk error occurred while opening the file.";
                break;
            case REGDB_E_CLASSNOTREG:
                errStr = "According to the registry, the type of file specified in AVIFileOpen does not have a handler to process it.";
                break;
            }


            return; // FIX
        }

        ZeroMemory(&m_AviStreamInfo,sizeof(AVISTREAMINFO));
        m_AviStreamInfo.fccType		= streamtypeVIDEO;
        m_AviStreamInfo.fccHandler	= mmioFOURCC('H','2','6','4');
        m_AviStreamInfo.dwScale		= 1;
        m_AviStreamInfo.dwRate		= framerate;  // frame rate = dwRate / dwScale
        m_AviStreamInfo.dwQuality	= 1;
        m_AviStreamInfo.dwSuggestedBufferSize = width*height*4;
        SetRect(&m_AviStreamInfo.rcFrame, 0, 0, width, height);
        wcscpy(m_AviStreamInfo.szName, L"Video Stream");

        if(FAILED(hr=AVIFileCreateStream(m_AviFile,&m_AviStream,&m_AviStreamInfo)))
        {
            m_AviFile->Release();
            return;    // FIX
        }


        BITMAPINFO bmpInfo;
        ZeroMemory(&bmpInfo,sizeof(BITMAPINFO));
        bmpInfo.bmiHeader.biPlanes		= 1;
        bmpInfo.bmiHeader.biWidth		= width;
        bmpInfo.bmiHeader.biHeight		= height;
        bmpInfo.bmiHeader.biCompression	= m_AviStreamInfo.fccHandler;
        bmpInfo.bmiHeader.biBitCount	= 32;
        bmpInfo.bmiHeader.biSize		= sizeof(BITMAPINFOHEADER);
        bmpInfo.bmiHeader.biSizeImage	= bmpInfo.bmiHeader.biWidth*bmpInfo.bmiHeader.biHeight*bmpInfo.bmiHeader.biBitCount/8;

        if(FAILED(hr=AVIStreamSetFormat(m_AviStream,0,(LPVOID)&bmpInfo, bmpInfo.bmiHeader.biSize)))
        {
            m_AviFile->Release();
            m_AviStream->Release();
            return; // FIX
        }
}

AviWriter::~AviWriter()
{
    m_AviStream->Release(); 

    m_AviFile->Release();
}


HRESULT AviWriter::AddFrame(char *frameBuffer, uint32_t numBytes,DWORD dwFlags, LONG frameNumber)
{
    HRESULT hr = AVIStreamWrite(m_AviStream, frameNumber, 1,
                                frameBuffer,
                                numBytes,
                                dwFlags,
                                NULL, NULL);

    return hr;
}
