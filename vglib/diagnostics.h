#ifndef DIAGNOSTICS_H
#define DIAGNOSTICS_H


#include <Windows.h>
#include <stdio.h>
#include <stdarg.h>
#include <TCHAR.h>
#include <string>
#include <iostream>
#include <fstream>


class Diagnostics
{
private:


public:


    static void DebugMessage(char* text, ...)
    {
        char outputBuffer[1024];
        va_list argptr;
        va_start(argptr, text);
        vsprintf_s(outputBuffer, sizeof(outputBuffer), text, argptr);
        va_end(argptr);

//        if(m_logging)
//        {
//            Log(outputBuffer);
//        }
//        else
        {
            OutputDebugStringA(outputBuffer);
            OutputDebugStringA("\n");
        }
    }

    static void DebugMessage(std::string str){

        char * writable = new char[str.size() + 1];
        std::copy(str.begin(), str.end(), writable);
        writable[str.size()] = '\0'; // don't forget the terminating 0

        DebugMessage("%s",writable);

        // free the string after finished using it
        delete[] writable;
    }


    static void InitLogFile(std::string filename)
    {
        m_logging = true;

        if(m_logFile.is_open()) m_logFile.close();

        m_logFile.open(filename,std::ios::trunc);
    }

    static void Log(std::string str)
    {
        if(!m_logFile.is_open()) return;

        m_logFile << str << "\n";
    }

    static void CloseLogFile()
    {
        m_logging = false;

        m_logFile.close();
    }


    static std::ofstream m_logFile;
    static bool m_logging;

};

#endif // DIAGNOSTICS_H
