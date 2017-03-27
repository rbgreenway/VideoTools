using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Detect_net
{
    public class Detector
    {
        const string DLL_NAME = "Detect_cpp.dll";


        [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Test")]
        // extern "C" DllExport int Test()
        static extern int Test();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateDetectors")]
        //extern "C" DllExport void CreateDetectors()
        static extern void CreateDetectors();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Shutdown")]
        //extern "C" DllExport void Shutdown()
        static extern void Shutdown();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "SetReferenceImage")]
        // extern "C" DllExport int SetReferenceImage(uint8_t *pImagedata, uint32_t imageWidth, uint32_t imageHeight, uint32_t roiX, uint32_t roiY, uint32_t roiW, uint32_t roiH)
        static extern int SetReferenceImage(byte[] pImagedata, UInt32 imageWidth, UInt32 imageHeight, UInt32 roiX, UInt32 roiY, UInt32 roiW, UInt32 roiH);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Surf_FindMatchDistance")]
        // extern "C" DllExport double Surf_FindMatchDistance(uint8_t *pImageData)
        static extern double Surf_FindMatchDistance(byte[] pImageData);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Surf_CalcImageCorrelation")]
        // extern "C" DllExport double Surf_CalcImageCorrelation(uint8_t *pImageData)
        static extern double Surf_CalcImageCorrelation(byte[] pImageData);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Akaze_FindMatchDistance")]
        // extern "C" DllExport double Akaze_FindMatchDistance(uint8_t *pImageData)
        static extern double Akaze_FindMatchDistance(byte[] pImageData);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "Akaze_CalcImageCorrelation")]
        // extern "C" DllExport double Akaze_CalcImageCorrelation(uint8_t *pImageData)
        static extern double Akaze_CalcImageCorrelation(byte[] pImageData);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.StdCall, EntryPoint = "TemplateMatch_CalculateImageCorrelation")]
        // extern "C" DllExport double TemplateMatch_CalculateImageCorrelation(uint8_t *testImageData)
        static extern double TemplateMatch_CalculateImageCorrelation(byte[] pImageData);

        bool m_Ready;

        public Detector() // constructor
        {
            m_Ready = false;
        }

        public int TestDll()
        {   // return 1 if working, -1 otherwise
            return Test();
        }

  
        public void InitDetector()
        {
            CreateDetectors();
            m_Ready = true;
        }

        public void ShutdownDetector()
        {
            m_Ready = false;
            Shutdown();
        }

        public int SetRefImage(byte[] pImagedata, UInt32 imageWidth, UInt32 imageHeight, UInt32 roiX, UInt32 roiY, UInt32 roiW, UInt32 roiH)
        {
            if (m_Ready)
                return SetReferenceImage(pImagedata, imageWidth, imageHeight, roiX, roiY, roiW, roiH);
            else
                return -1;
        }

        public double SurfCalcMatchDistance(byte[] pImageData)
        {
            if (m_Ready)
                return Surf_FindMatchDistance(pImageData);
            else
                return -1.0;
        }

        public double SurfCalcCorrelation(byte[] pImageData)
        {
            if (m_Ready)
                return Surf_CalcImageCorrelation(pImageData);
            else
                return -1.0;
        }


        public double AkazeCalcMatchDistance(byte[] pImageData)
        {
            if (m_Ready)
                return Akaze_FindMatchDistance(pImageData);
            else
                return -1.0;
        }

        public double AkazeCalcCorrelation(byte[] pImageData)
        {
            if (m_Ready)
                return Akaze_CalcImageCorrelation(pImageData);
            else
                return -1.0;
        }


        public double CalcTemplateMatch(byte[] pImageData)
        {
            if (m_Ready)
                return TemplateMatch_CalculateImageCorrelation(pImageData);
            else
                return -1.0;
        }
    }
}
