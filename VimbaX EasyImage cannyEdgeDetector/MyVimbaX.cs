# define USE_OPEN_EVISION
# define USE_WPF

#if USE_OPEN_EVISION
    using Euresys.Open_eVision;
# endif

#if USE_WPF
    using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
    using System.Windows.Media.Imaging;
#else
    using System.Drawing.Imaging;
#endif

// VimbaX Driver
using VmbNET;

// +-------------------------------- DISCLAIMER ---------------------------------+
// |                                                                             |
// | This application program is provided to you free of charge as an example.   |
// | EURESYS' Terms and Conditions shall apply to the use of this application    |
// | program, subject to the following derogations: you are authorized to use,   |
// | modify and distribute the source code of this application program in any    |
// | way you find useful provided the source code or modified source code is     |
// | used solely in conjunction with EURESYS' Open eVision, and distributed      |
// | under the same license.                                                     |
// | Despite the considerable efforts of Euresys personnel to create a usable    |
// | example, you should not assume that this program is error-free or suitable  |
// | for any purpose whatsoever.                                                 |
// | EURESYS does not give any representation, warranty or undertaking that this |
// | program is free of any defect or error or suitable for any purpose. EURESYS |
// | shall not be liable, in contract, in torts or otherwise, for any damages,   |
// | loss, costs, expenses or other claims for compensation, including those     |
// | asserted by third parties, arising out of or in connection with the use of  |
// | this program.                                                               |
// |                                                                             |
// +-----------------------------------------------------------------------------+
//
// Brief description of this program
//
// myCamera class module for Euresys Open eVision EImageC24, EImageBW8
//
// Created date : April 15th, 2025
// Descriptor   : Kazuhiro Tanaka (Support Manager)
//                Euresys Japan K.K.   
//                kazuhiro.tanaka@euresys.com
//

namespace VimbaX_EasyImage_cannyEdgeDetector
{
    internal class myCamera
    {
        //VimbaX
        //Interface for accessing the entirety of VmbC.
        private IVmbSystem myVmbSystem;
        //Interface for accessing an open camera.
        private IOpenCamera myOpenedCamera; //Camera Automatically Close When .Dipose()
        //Interface for acquiring and capturing frames asynchronously.
        private IAcquisition myAcquisition;
        private IFrame PreviousFrame;
        private double TICK_FREQUENCY = 1e9;

        private ulong PreviousFrameCount = 0;
        public bool bInitilizeDone = false;
        public bool bAcquisition = false;
        private bool bOperation = false;
        public int m_Buffer_Size = 10;
        public string m_Device_Interface = "None";
        public string m_Device_Search_Type = "None";
        public string m_Device_search_Value = "";

        public string m_Camera_Vendor = "";
        public string m_Camera_Model = "";
        public ulong m_Image_Width;
        public ulong m_Image_Height;
        public string m_Image_Format = "";
        public float m_Image_Aspect;
        private int m_Image_BufferPitch = 1;
        public uint m_DropFrameCount = 0;
        public double m_Frame_Rate = 0;
        public ulong m_Image_FrameID = 0;
        public ulong m_Image_TimeStamp = 0;
        private const int cEXPOSURE_TIME = 20000; // ExposureTime initial value

        private System.Diagnostics.Stopwatch ProcessingTime = new System.Diagnostics.Stopwatch();
        public TimeSpan tsPreProcessingTime;

#if USE_WPF
        public WriteableBitmap imageBitmap;
#else
        public System.Drawing.Bitmap imageBitmap;
        private ColorPalette myMonoColorPalette;
#endif

#if USE_OPEN_EVISION
        public EImageC24 imageC24;
        public EImageBW8 imageBW8;
#endif

        public bool Innitialize()
        {
            bool bDone = false;

            if (AVT_VimbaX_Init())
            {
                initBitmap();
                bInitilizeDone = true;
                bDone = true;
            }
            return bDone;
        }

        private void set_bufferPitch()
        {
            int bufferPitch = 1;
            if ((m_Image_Format == "RGB10") || (m_Image_Format == "RGB12") || (m_Image_Format == "RGB14") || (m_Image_Format == "RGB16"))
                bufferPitch = 6;
            else if (m_Image_Format == "RGB8")
                bufferPitch = 3;
            else if ((m_Image_Format.Contains("Bayer") && m_Image_Format.Contains("10")) ||
                    (m_Image_Format.Contains("Bayer") && m_Image_Format.Contains("12")) ||
                    (m_Image_Format.Contains("Bayer") && m_Image_Format.Contains("14")) ||
                    (m_Image_Format.Contains("Bayer") && m_Image_Format.Contains("16")))
                bufferPitch = 6;
            else if (m_Image_Format.Contains("Bayer") && m_Image_Format.Contains("8"))
                bufferPitch = 3;
            else if ((m_Image_Format == "Mono10") || (m_Image_Format == "Mono12") || (m_Image_Format == "Mono14") || (m_Image_Format == "Mono16"))
                bufferPitch = 2;
            else if (m_Image_Format == "Mono8")
                bufferPitch = 1;
            m_Image_BufferPitch = bufferPitch;
        }

        private void initBitmap()
        {
            if (m_Image_Format.Contains("Mono"))
            {
                if (m_Image_Format == "Mono8")
#if USE_WPF
                    imageBitmap = new WriteableBitmap((int)m_Image_Width, (int)m_Image_Height, 96, 96, System.Windows.Media.PixelFormats.Gray8, null);
#else
                    {
                        imageBitmap = new System.Drawing.Bitmap((int)m_Image_Width, (int)m_Image_Height, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
                        // Create a Monochrome palette (only once)
                        if (myMonoColorPalette == null)
                        {
                            Bitmap monoBitmap = new Bitmap(1, 1, PixelFormat.Format8bppIndexed);
                            myMonoColorPalette = monoBitmap.Palette;
                            for (int i = 0; i < 256; i++)
                                myMonoColorPalette.Entries[i] = Color.FromArgb(i, i, i);
                        }
                    }
#endif
                else
#if USE_WPF
                    imageBitmap = new WriteableBitmap((int)m_Image_Width, (int)m_Image_Height, 96, 96, System.Windows.Media.PixelFormats.Gray16, null);
#else
                    imageBitmap = new System.Drawing.Bitmap((int)m_Image_Width, (int)m_Image_Height, System.Drawing.Imaging.PixelFormat.Format16bppGrayScale);
#endif
            }
            else
            {
                if (m_Image_Format.Contains("8"))
#if USE_WPF
                    imageBitmap = new WriteableBitmap((int)m_Image_Width, (int)m_Image_Height, 96, 96, System.Windows.Media.PixelFormats.Rgb24, null);
#else
                    imageBitmap = new System.Drawing.Bitmap((int)m_Image_Width, (int)m_Image_Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
#endif
                else
#if USE_WPF
                    imageBitmap = new WriteableBitmap((int)m_Image_Width, (int)m_Image_Height, 96, 96, System.Windows.Media.PixelFormats.Rgb48, null);
#else
                    imageBitmap = new System.Drawing.Bitmap((int)m_Image_Width, (int)m_Image_Height, System.Drawing.Imaging.PixelFormat.Format48bppRgb);
#endif
            }
        }

        private bool Camera_ParameterSet()
        {
            bool bDone = false;
            if (myOpenedCamera != null)
            {
                try
                {
                    m_Camera_Vendor = GetParam("DeviceVendorName");
                    m_Camera_Model = GetParam("DeviceModelName");
                    m_Image_Width = Convert.ToUInt32(GetParam("Width"));
                    m_Image_Height = Convert.ToUInt32(GetParam("Height"));
                    m_Image_Format = GetParam("PixelFormat");
                    set_bufferPitch();
                    m_Image_Aspect = (float)m_Image_Width / (float)m_Image_Height;
                    bDone = true;
                    make_task();
                }
                catch (Exception exc)
                { }
            }
            return bDone;
        }

        private bool select_IF(ICamera camera)
        {
            bool bDone = false;
            if (m_Device_Interface.Contains("USB3") && camera.Interface.Id.Contains("USB"))
                bDone = true;
            else if (m_Device_Interface.Contains("GigE") && camera.Interface.Id.Contains("GigE"))
                bDone = true;
            else if (m_Device_Interface.Contains("CoaXPress") && camera.Interface.Id.Contains("Coaxlink"))
                bDone = true;
            return bDone;
        }

        private bool select_Device(ICamera camera)
        {
            bool bDone = false;
            if ((m_Device_Search_Type == "Vendor") && camera.Name.Contains(m_Device_search_Value))
                bDone = true;
            else if ((m_Device_Search_Type == "Model") && camera.Name.Contains(m_Device_search_Value))
                bDone = true;
            else if ((m_Device_Search_Type == "Serial") && camera.Serial.Contains(m_Device_search_Value))
                bDone = true;
            else if ((m_Device_Search_Type == "UserID") && camera.Name.Contains(m_Device_search_Value))
                bDone = true;
            else if (m_Device_Search_Type == "None")
                bDone = true;
            return bDone;
        }

        private bool Select_Camera()
        {
            bool bDone = false;
            // get all cameras found by Vimba X
            var cameras = myVmbSystem.GetCameras();
            if (cameras.Count != 0)
                bDone = true;
            if (bDone)
            {
                bDone = false;
                foreach (var camera in cameras)
                {
                    if (!bDone)
                    {
                        if (select_IF(camera) && select_Device(camera))
                        {
                            myOpenedCamera = camera.Open();
                            bDone = true;
                        }
                    }
                }
            }
            return bDone;
        }

        private bool AVT_VimbaX_Init()
        {
            bool bDone = false;
            try
            {
                // API startup (loads transport layers)
                // startup Vimba X
                myVmbSystem = IVmbSystem.Startup();
                bDone = true;
            }
            catch (Exception exc)
            {
                bDone = false;
            }
            if (bDone)
                bDone = Select_Camera();
            if (bDone)
            {
                if (!Camera_ParameterSet())
                    bDone = false;
            }
            return bDone;
        }

        private void make_task()
        {
            // Register an event handler for the "FrameReceived" event
            // Basically, this is the callback you receive when frames are filled.
            myOpenedCamera.FrameReceived += (_, frameReceivedEventArgs) =>
            {
                using (var frame = frameReceivedEventArgs.Frame)
                {
                    if (frame != null)
                    {
                        if (PreviousFrame != null)
                        {
                            ulong uintaval = (ulong)frame.Timestamp - (ulong)PreviousFrame.Timestamp;
                            m_Frame_Rate = TICK_FREQUENCY / uintaval;
                        }
                        PreviousFrame = frame;
                    }
                }
            }; // IDisposable: leaving the scope will automatically requeue
        }

        private string GetDeviceVendorName()
        {
            string sValue = "";
            try
            {
                IStringFeature sModeLName = myOpenedCamera.Features.DeviceVendorName;
                sValue = sModeLName.Value;
            }
            catch
            { }
            return sValue;
        }

        private string GetDeviceModelName()
        {
            string sValue = "";
            try
            {
                IStringFeature sModeLName = myOpenedCamera.Features.DeviceModelName;
                sValue = sModeLName.Value;
            }
            catch
            { }
            return sValue;
        }

        private string GetWidth()
        {
            string sValue = "";
            try
            {
                IIntFeature iWidth = myOpenedCamera.Features.Width;
                int iValue = (int)iWidth.Value;
                sValue = iValue.ToString();
            }
            catch
            { }
            return sValue;
        }

        private string GetWidthMax()
        {
            string sValue = "";
            try
            {
                IIntFeature iWidth = myOpenedCamera.Features.Width;
                int iValue = (int)iWidth.Maximum;
                sValue = iValue.ToString();
            }
            catch
            { }
            return sValue;
        }

        private string GetWidthMin()
        {
            string sValue = "";
            try
            {
                IIntFeature iWidth = myOpenedCamera.Features.Width;
                int iValue = (int)iWidth.Minimum;
                sValue = iValue.ToString();
            }
            catch
            { }
            return sValue;
        }

        private string GetWidthInc()
        {
            string sValue = "";
            try
            {
                IIntFeature iWidth = myOpenedCamera.Features.Width;
                int iValue = (int)iWidth.Increment;
                sValue = iValue.ToString();
            }
            catch
            { }
            return sValue;
        }

        private string GetHeight()
        {
            string sValue = "";
            try
            {
                IIntFeature iHeight = myOpenedCamera.Features.Height;
                int iValue = (int)iHeight.Value;
                sValue = iValue.ToString();
            }
            catch
            { }
            return sValue;
        }

        private string GetHeightMax()
        {
            string sValue = "";
            try
            {
                IIntFeature iHeight = myOpenedCamera.Features.Height;
                int iValue = (int)iHeight.Maximum;
                sValue = iValue.ToString();
            }
            catch
            { }
            return sValue;
        }

        private string GetHeightMin()
        {
            string sValue = "";
            try
            {
                IIntFeature iHeight = myOpenedCamera.Features.Height;
                int iValue = (int)iHeight.Minimum;
                sValue = iValue.ToString();
            }
            catch
            { }
            return sValue;
        }

        private string GetHeightInc()
        {
            string sValue = "";
            try
            {
                IIntFeature iHeight = myOpenedCamera.Features.Height;
                int iValue = (int)iHeight.Increment;
                sValue = iValue.ToString();
            }
            catch
            { }
            return sValue;
        }

        private string GetPixelFormat()
        {
            string sValue = "";
            try
            {
                IEnumFeature PixelFormatFeature = myOpenedCamera.Features.PixelFormat;
                sValue = PixelFormatFeature.Value;
            }
            catch
            { }
            return sValue;
        }

        private string GetExposureMode()
        {
            string sValue = "";
            try
            {
                IEnumFeature fExposureMode = myOpenedCamera.Features.ExposureMode;
                sValue = fExposureMode.Value;
            }
            catch
            { }
            return sValue;
        }

        private string GetExposureTime()
        {
            string sValue = "";
            try
            {
                IFloatFeature fExposureTime = myOpenedCamera.Features.ExposureTime;
                double fValue = fExposureTime.Value;
                sValue = fValue.ToString();
            }
            catch
            { }
            return sValue;
        }

        private string GetExposureTimeMax()
        {
            string sValue = "";
            try
            {
                IFloatFeature fExposureTime = myOpenedCamera.Features.ExposureTime;
                double fValue = fExposureTime.Maximum;
                sValue = fValue.ToString();
            }
            catch
            { }
            return sValue;
        }

        private string GetExposureTimeMin()
        {
            string sValue = "";
            try
            {
                IFloatFeature fExposureTime = myOpenedCamera.Features.ExposureTime;
                double fValue = fExposureTime.Minimum;
                sValue = fValue.ToString();
            }
            catch
            { }
            return sValue;
        }

        public string GetParam(string sCommand)
        {
            string sTemp = "";
            if (sCommand == "DeviceVendorName")
                sTemp = GetDeviceVendorName();
            else if (sCommand == "DeviceModelName")
                sTemp = GetDeviceModelName();
            else if (sCommand == "Width")
                sTemp = GetWidth();
            else if (sCommand == "Width.Max")
                sTemp = GetWidthMax();
            else if (sCommand == "Width.Min")
                sTemp = GetWidthMin();
            else if (sCommand == "Width.Inc")
                sTemp = GetWidthInc();
            else if (sCommand == "Height")
                sTemp = GetHeight();
            else if (sCommand == "Height.Max")
                sTemp = GetHeightMax();
            else if (sCommand == "Height.Min")
                sTemp = GetHeightMin();
            else if (sCommand == "Height.Inc")
                sTemp = GetHeightInc();
            else if (sCommand == "PixelFormat")
                sTemp = GetPixelFormat();
            else if (sCommand == "ExposureMode")
                sTemp = GetExposureMode();
            else if (sCommand == "ExposureTime")
                sTemp = GetExposureTime();
            else if (sCommand == "ExposureTime.Max")
                sTemp = GetExposureTimeMax();
            else if (sCommand == "ExposureTime.Min")
                sTemp = GetExposureTimeMin();
            return sTemp;
        }

        private bool SetWidth(string sValue)
        {
            bool bDone = false;
            try
            {
                int iValue = Convert.ToInt32(sValue);
                myOpenedCamera.Features.Width = iValue;
                bDone = true;
            }
            catch
            { }
            return bDone;
        }

        private bool SetHeight(string sValue)
        {
            bool bDone = false;
            try
            {
                int iValue = Convert.ToInt32(sValue);
                myOpenedCamera.Features.Height = iValue;
                bDone = true;
            }
            catch
            { }
            return bDone;
        }

        private bool SetPixelFormat(string sValue)
        {
            bool bDone = false;
            try
            {
                myOpenedCamera.Features.PixelFormat = sValue;
                m_Image_Format = sValue;
                set_bufferPitch();
                initBitmap();
                bDone = true;
            }
            catch
            { }
            return bDone;
        }

        private bool SetExposureMode(string sValue)
        {
            bool bDone = false;
            try
            {
                myOpenedCamera.Features.ExposureMode = sValue;
                bDone = true;
            }
            catch
            { }
            return bDone;
        }

        private bool SetExposureTime(string sValue)
        {
            bool bDone = false;
            try
            {
                double dValue = Convert.ToDouble(sValue);
                myOpenedCamera.Features.ExposureTime = dValue;
                bDone = true;
            }
            catch
            { }
            return bDone;
        }

        public bool SetParam(string sCommand, string sValue)
        {
            bool bDone = false;
            try
            {
                if (sCommand == "Width")
                    bDone = SetWidth(sValue);
                else if (sCommand == "Height")
                    bDone = SetHeight(sValue);
                else if (sCommand == "PixelFormat")
                    bDone = SetPixelFormat(sValue);
                else if (sCommand == "ExposureMode")
                    bDone = SetExposureMode(sValue);
                else if (sCommand == "ExposureTime")
                    bDone = SetExposureTime(sValue);
            }
            catch 
            { }
            return bDone;
        }

#if USE_OPEN_EVISION
//
#else
#if USE_WPF
        private static BitmapSource getImageBufferBW8(int width, int height, IntPtr iAddress, int pitch)
        {
            BitmapSource newBitmapSorce = BitmapSource.Create(
                            width,
                            height,
                            96,
                            96,
                            PixelFormats.Gray8,
                            null,
                            iAddress,
                            width * pitch * height,
                            width * pitch);
            newBitmapSorce.Freeze();
            return newBitmapSorce;
        }

        private static BitmapSource getImageBufferC24(int width, int height, IntPtr iAddress, int pitch)
        {
            BitmapSource newBitmapSorce = BitmapSource.Create(
                            width,
                            height,
                            96,
                            96,
                            PixelFormats.Rgb24,
                            null,
                            iAddress,
                            width * pitch * height,
                            width * pitch);
            newBitmapSorce.Freeze();
            return newBitmapSorce;
        }
#endif
#endif

        public void get_Image()
        {
            // + MulticamAdvancedWaitSignal Sample Program
            try
            {
                if (PreviousFrame != null)
                {
                    IFrame frame = PreviousFrame;
                    ProcessingTime.Reset();
                    ProcessingTime.Start();
                    // Get a pointer to the back buffer.
                    IFrame.PixelFormatValue newFormat = frame.PixelFormat;
                    string newPixelFormat = newFormat.ToString();
                    if (newPixelFormat.Contains("Mono"))
                    {
#if USE_OPEN_EVISION
                        imageBW8 = new EImageBW8((int)m_Image_Width, (int)m_Image_Height);
                        imageBW8.SetImagePtr((int)frame.Width, (int)frame.Height, frame.ImageData);
#if USE_WPF
                        IntPtr pBackBufferS = imageBitmap.BackBuffer;
                        imageBitmap.Lock();
                        EImageBW8 newImageBW8 = new EImageBW8((int)m_Image_Width, (int)m_Image_Height);
                        newImageBW8.SetImagePtr((int)m_Image_Width, (int)m_Image_Height, pBackBufferS);
                        EasyImage.Copy(imageBW8, newImageBW8);
                        imageBitmap.AddDirtyRect(new Int32Rect(0, 0, (int)m_Image_Width, (int)m_Image_Height));
                        imageBitmap.Unlock();
#endif
#else
#if USE_WPF
                        imageBitmap = new WriteableBitmap(getImageBufferBW8((int)m_Image_Width, (int)m_Image_Height, (IntPtr)frame.Buffer, (int)m_Image_BufferPitch));
#endif
#endif
#if !USE_WPF
                        imageBitmap = new Bitmap((int)m_Image_Width, (int)m_Image_Height, (int)m_Image_Width * m_Image_BufferPitch, System.Drawing.Imaging.PixelFormat.Format8bppIndexed, frame.Buffer);
                        // Set the Monochrome Color Palette
                        imageBitmap.Palette = myMonoColorPalette;
#endif
                    }
                    else
                    {
#if USE_OPEN_EVISION
                        imageC24 = new EImageC24((int)m_Image_Width, (int)m_Image_Height);
                        imageC24.SetImagePtr((int)frame.Width, (int)frame.Height, frame.ImageData);
                        imageBW8 = new EImageBW8((int)m_Image_Width, (int)m_Image_Height);
                        EasyImage.Oper(EArithmeticLogicOperation.Copy, new EBW8(0), imageBW8);
                        EasyImage.Convert(imageC24, imageBW8);
#if USE_WPF
                        IntPtr pBackBufferS = imageBitmap.BackBuffer;
                        imageBitmap.Lock();
                        EImageC24 newImageC24 = new EImageC24((int)m_Image_Width, (int)m_Image_Height);
                        newImageC24.SetImagePtr((int)m_Image_Width, (int)m_Image_Height, pBackBufferS);
                        EasyImage.Copy(imageC24, newImageC24);
                        imageBitmap.AddDirtyRect(new Int32Rect(0, 0, (int)m_Image_Width, (int)m_Image_Height));
                        imageBitmap.Unlock();
#endif
#else
#if USE_WPF
                        imageBitmap = new WriteableBitmap(getImageBufferC24((int)m_Image_Width, (int)m_Image_Height, (IntPtr)frame.Buffer, (int)m_Image_BufferPitch));
#endif
#endif
#if !USE_WPF
                        imageBitmap = new Bitmap((int)m_Image_Width, (int)m_Image_Height, (int)m_Image_Width* m_Image_BufferPitch, System.Drawing.Imaging.PixelFormat.Format24bppRgb, frame.Buffer);
#endif
                    }
                    tsPreProcessingTime = ProcessingTime.Elapsed;
                    if ((frame != null) && (frame.Id > (PreviousFrameCount + 1)))
                        m_DropFrameCount += (uint)(frame.Id - (PreviousFrameCount + 1));
                    PreviousFrameCount = frame.Id;
                    m_Image_FrameID = PreviousFrameCount;
                    m_Image_TimeStamp = frame.Timestamp;
                }
            }
            catch (System.Exception exc)
            {
                
            }
            // - MulticamAdvancedWaitSignal Sample Program
        }

        public void StartStream()
        {
            //Start Acquisition
            myAcquisition = myOpenedCamera.StartFrameAcquisition();
            bAcquisition = true;
        }

        public void StopStream()
        {
            //Stop Acquisition
            myAcquisition.Dispose();
            PreviousFrame.Dispose();
            bAcquisition = false;
        }

        public void Close()
        {
            if (bAcquisition)
            {
                bAcquisition = false;
            }
            //Dispose After Usage
            if (myAcquisition != null)
            {
                myAcquisition.Dispose();
                myAcquisition = null;
            }
            myOpenedCamera.Dispose();
            myOpenedCamera = null;
            myVmbSystem.Dispose();
            myVmbSystem = null;
        }
    }
}
