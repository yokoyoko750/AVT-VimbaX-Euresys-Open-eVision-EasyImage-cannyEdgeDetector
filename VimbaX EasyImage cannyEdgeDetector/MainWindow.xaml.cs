using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
// Open eVision
using Euresys.Open_eVision;

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
// The camera is used for free running mode.
//
// In Free running Capture Mode,
// Use Windows' System.Windows.Threading.DispatcherTimer to perform Save Image
// at each repetition cycle.
//
// When not using Auto Capture Mode
// If you press Save Image during Acquisition Active, Acquisition Stop will be
// performed once, File Save will be performed, and then Acquisition Start will
// be performed again.
//
// Pressing Save Image during Acquisition Stop will disable the Save Image button
// after performing a File Save and will not become active until the next new
// image is acquired.
//
// GroupBox_Attribute has an initial value of None.
// The file format is Image_[3-digit file number].png
// If you select Good, NG1, NG2
// The file format is Image_[Good/NG1/GG2]_[3-digit file number].png
//
// ExposureTime settings need to be changed to match the camera you are using.
// In particular, some cameras use the old format ExposureTimeAbs.
// Set the ExposureTime command name to Exposure in Custom_List.dat.
//
// Created date : February 20, 2024
// Descriptor   : Kazuhiro Tanaka (Support Manager)
//                Euresys Japan K.K.   
//                kazuhiro.tanaka@euresys.com
//

enum State
{
    Ready,      // Before starting calculation
//  capturing,  // Acquiring images
    NewProcess, // Start image processing
    Processing, // In the middle of calculation
//  NewDisplay, // Start Display processing
//  Displaying  // Displaying results
}

namespace VimbaX_EasyImage_cannyEdgeDetector
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string FILE_CUSTOM_LIST = "Custom_List.dat";
        private string DEVICE_INTERFACE_TYPE = "USB3 Vision";
        private string DEVICE_SEARCH_TYPE = "None";
        private string DEVICE_SEARCH_VALUE = "AVT";

        private const float EXPOSURE_TIME_MIN_VALUE = 5.0f;     // ExposureTime (us) Minimum
        private const float EXPOSURE_TIME_MAX_VALUE = 100000f;  // ExposureTime (us) Maximum
        private string sSaveFile_Attribute1 = "Good";
        private string sSaveFile_Attribute2 = "NG1";
        private string sSaveFile_Attribute3 = "NG2";

        myCamera myCamera = new myCamera();
        private const int NB_BUFFERS = 10;

        // File Setting
        private string sFileDirectory = "c:\\temp\\";
        private string sFileName = "Img";
        private string sFileType = ".png";
        private int mFileNumber = 0;

        private static State sThreadState;
        private bool bInitilizeDone = false;
        private bool bCapturing = false;
        private bool bsLider_Exposure = false;
        private bool bsLider_Interval = false;
        private bool bsLider_ProcessCores = false;
        private bool bsLider_EasyImage_Adjust1 = false;
        private bool bsLider_EasyImage_Adjust2 = false;
        private bool bsLider_EasyImage_Adjust3 = false;

        private readonly System.Windows.Threading.DispatcherTimer dispatcherTimerGettingImage = new System.Windows.Threading.DispatcherTimer();
        private readonly System.Windows.Threading.DispatcherTimer dispatcherTimerAutoImageSave = new System.Windows.Threading.DispatcherTimer();
        private bool bAcquisition = false;

        private UInt64 m_Image_Width;
        private UInt64 m_Image_Height;
        private String m_Image_format;

        // Open eVision
        private const int PROCESS_INTERVAL = 20;
        private System.Diagnostics.Stopwatch ProcessingTime = new System.Diagnostics.Stopwatch();
    //  private TimeSpan tsPreProcessingTime;
        private TimeSpan tsMainProcessingTime;
        private TimeSpan tsDisplayProcessingTime;
        private TimeSpan tsPreProcessingAverage;
        private TimeSpan tsMainProcessingAverage;
        private TimeSpan tsDisplayProcessingAverage;
        private WriteableBitmap imageBitmapResult;

        private bool bProcessing = false;
        private bool bDrawing = false;

        private ECannyEdgeDetector MycannyEdgeDetector = new ECannyEdgeDetector(); // ECannyEdgeDetector instance
        private float m_HighThreshold = 128;
        private float m_LowThreshold = 0;
        private float m_SmoothingScale = 0;

        public MainWindow()
        {
            InitializeComponent();

            load_Custom_List();

            Radio_Attribute1.Content = sSaveFile_Attribute1;
            Radio_Attribute2.Content = sSaveFile_Attribute2;
            Radio_Attribute3.Content = sSaveFile_Attribute3;

            if (set_Grabber())
            {
                bInitilizeDone = true;
                CameraImage.Source = myCamera.imageBitmap;
                dispatcherTimerAutoImageSave.Tick += new EventHandler(GenerateAutoSaveTimming);
                dispatcherTimerAutoImageSave.Interval = TimeSpan.FromMilliseconds((double)(sLider_IntervalTime.Value * 1000));

                tButton_Operation.IsEnabled = true;
                tButton_Operation.Foreground = new SolidColorBrush(Colors.Black);
                bAcquisition = false;

                GroupBox_Files_attribute(true);
                mFileNumber--;
                TEXT_FileIndex.Text = mFileNumber.ToString();
                if (!System.IO.File.Exists(sFileDirectory))
                {
                    System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(sFileDirectory);
                    di.Create();
                    TEXT_SaveDirectoryv.Text = sFileDirectory;
                }
                dispatcherTimerGettingImage.Tick += new EventHandler(GettingImage);
                dispatcherTimerGettingImage.Interval = TimeSpan.FromMilliseconds(PROCESS_INTERVAL);
                if (!Initialize_Open_eVision())
                {
                    MessageBox.Show("No license for Open eVision EasyImage.\nExit this program.");
                    Close();
                }
            }
            else
            {
                MessageBox.Show("No Camera found.\nExit this program.");
                Close();
            }
        }

        private bool load_Custom_List()
        {
            bool bSucess = false;

            if (System.IO.File.Exists(FILE_CUSTOM_LIST))
            {
                using (StreamReader file = new StreamReader(FILE_CUSTOM_LIST, Encoding.GetEncoding("UTF-8")))
                {
                    if (file != null)
                    {
                        while (file.Peek() != -1)
                        {
                            // Get Camera File Name
                            string sLang = file.ReadLine();
                            if (sLang.Contains("InterfaceType"))
                            {
                                sLang = sLang.Substring(sLang.IndexOf(@"""") + 1, sLang.LastIndexOf(@"""") - sLang.IndexOf(@"""") - 1);
                                if ((sLang == "GigE Vision") || (sLang == "USB3 Vision") || (sLang == "CoaXPress") || (sLang == "CameraLink"))
                                    DEVICE_INTERFACE_TYPE = sLang;
                                bSucess = true;
                            }
                            else if (sLang.Contains("SearchCamera"))
                            {
                                sLang = sLang.Substring(sLang.IndexOf(@"""") + 1, sLang.LastIndexOf(@"""") - sLang.IndexOf(@"""") - 1);
                                if ((sLang == "Vendor") || (sLang == "Model") || (sLang == "Serial") || (sLang == "UserID"))
                                    DEVICE_SEARCH_TYPE = sLang;
                                bSucess = true;
                            }
                            else if (sLang.Contains("SearchValue"))
                            {
                                sLang = sLang.Substring(sLang.IndexOf(@"""") + 1, sLang.LastIndexOf(@"""") - sLang.IndexOf(@"""") - 1);
                                DEVICE_SEARCH_VALUE = sLang;
                                bSucess = true;
                            }
                            else if (sLang.Contains("Save_Attribute1"))
                            {
                                sLang = sLang.Substring(sLang.IndexOf(@"""") + 1, sLang.LastIndexOf(@"""") - sLang.IndexOf(@"""") - 1);
                                if (sLang != "")
                                    sSaveFile_Attribute1 = sLang;
                                bSucess = true;
                            }
                            else if (sLang.Contains("Save_Attribute2"))
                            {
                                sLang = sLang.Substring(sLang.IndexOf(@"""") + 1, sLang.LastIndexOf(@"""") - sLang.IndexOf(@"""") - 1);
                                if (sLang != "")
                                    sSaveFile_Attribute2 = sLang;
                                bSucess = true;
                            }
                            else if (sLang.Contains("Save_Attribute3"))
                            {
                                sLang = sLang.Substring(sLang.IndexOf(@"""") + 1, sLang.LastIndexOf(@"""") - sLang.IndexOf(@"""") - 1);
                                if (sLang != "")
                                    sSaveFile_Attribute3 = sLang;
                                bSucess = true;
                            }
                        }
                    }
                }
            }
            return bSucess;
        }

        private bool set_Grabber()
        {
            bool dDone = false;
            myCamera.m_Device_Interface = DEVICE_INTERFACE_TYPE;
            myCamera.m_Device_Search_Type = DEVICE_SEARCH_TYPE;
            myCamera.m_Device_search_Value = DEVICE_SEARCH_VALUE;
            myCamera.m_Buffer_Size = NB_BUFFERS;
            if (myCamera.Innitialize())
            {
                myCamera.SetParam("PixelFormat", "Mono8");
                m_Image_Width = myCamera.m_Image_Width;
                m_Image_Height = myCamera.m_Image_Height;
                m_Image_format = myCamera.m_Image_Format;

                CameraVender.Text = myCamera.m_Camera_Vendor;
                CameraModel.Text = myCamera.m_Camera_Model;
                ImageWidth.Text = m_Image_Width.ToString();
                ImageHeight.Text = m_Image_Height.ToString();
                ImagePixelFormat.Text = m_Image_format;
                dDone = true;
                try
                {
                    myCamera.SetParam("ExposureMode", "Timed");
                }
                catch
                {
                    MessageBox.Show("This camera could not set ExposureMode = Timed.\n");
                }
                try
                {
                    double iExposureMin = Convert.ToDouble(myCamera.GetParam("ExposureTime.Min"));
                    if (iExposureMin < EXPOSURE_TIME_MIN_VALUE)
                        iExposureMin = EXPOSURE_TIME_MIN_VALUE;
                    sLider_ExposureTime.Minimum = iExposureMin;
                    double iExposureMax = Convert.ToDouble(myCamera.GetParam("ExposureTime.Max"));
                    if (iExposureMax > EXPOSURE_TIME_MAX_VALUE)
                        iExposureMax = EXPOSURE_TIME_MAX_VALUE;
                    sLider_ExposureTime.Maximum = iExposureMax;
                    double iExposureTime = Convert.ToDouble(myCamera.GetParam("ExposureTime"));
                    if (iExposureMax < iExposureTime)
                        iExposureTime = iExposureMax;
                    sLider_ExposureTime.Value = iExposureTime;
                    label_ExposureTime.Text = iExposureTime.ToString();
                    myCamera.SetParam("ExposureTime", iExposureTime.ToString());
                    // if support ExposureTime
                    GroupBox_Exposure.IsEnabled = true;
                    GroupBox_Exposure.Foreground = new SolidColorBrush(Colors.White);
                    CheckBox_ExposureTime.IsEnabled = true;
                    CheckBox_ExposureTime.Foreground = new SolidColorBrush(Colors.White);
                    GroupBox_Exposure_attribute(true);
                    CameraImage.IsEnabled = true;
                }
                catch
                {
                    MessageBox.Show("This camera does not support [ExposureTime] command.\n");
                }
            }

            return dDone;
        }

        private bool Initialize_Open_eVision()
        {
            bool bDone = false;
            if (Easy.CheckLicense(Euresys.Open_eVision.LicenseFeatures.Features.EasyObject))
            {
                try
                {
                    // Multi core test
                    int iCore = Euresys.Open_eVision.Easy.NumberOfAvailableProcessorCores;
                    sLider_ProcessCores.Maximum = iCore;
                    sLider_ProcessCores.Value = iCore / 2;
                    Easy.MaxNumberOfProcessingThreads = iCore / 2;
                    label_ProcessCores.Text = (iCore / 2).ToString();

                    MycannyEdgeDetector.ThresholdingMode = ECannyThresholdingMode.Absolute;
                    sLider_EasyImage_Adjust1.Value = m_HighThreshold;
                    label_EasyImage_Adjust1.Text = m_HighThreshold.ToString();
                    MycannyEdgeDetector.HighThreshold = m_HighThreshold;
                    sLider_EasyImage_Adjust2.Value = m_LowThreshold;
                    label_EasyImage_Adjust2.Text = m_LowThreshold.ToString();
                    MycannyEdgeDetector.LowThreshold = m_LowThreshold;

                    tButton_Operation.IsEnabled = true;
                    tButton_Operation.Foreground = new SolidColorBrush(Colors.Black);
                    GroupBox_Open_eVision.IsEnabled = true;
                    GroupBox_Open_eVision.Foreground = new SolidColorBrush(Colors.White);
                    CheckBox_OpeneVision_Enable.IsEnabled = true;
                    CheckBox_OpeneVision_Enable.Foreground = new SolidColorBrush(Colors.White);
                    // Load Target is necessary, so this part should be set true.
                    CheckBox_OpeneVision_Enable.IsChecked = false;
                    GroupBox_Open_eVision_attribute(false);
                    bDone = true;
                }
                catch (Exception)
                {
                    DisposeUnmanagedResources();
                    throw;
                }
            }
            return bDone;
        }

        private void GroupBox_Exposure_attribute(bool bActive)
        {
            if (bActive)
            {
                GroupBox_Exposure.IsEnabled = true;
                GroupBox_Exposure.Foreground = new SolidColorBrush(Colors.White);
                CheckBox_ExposureTime.Foreground = new SolidColorBrush(Colors.White);
            }
            else
            {
                GroupBox_Exposure.IsEnabled = false;
                GroupBox_Exposure.Foreground = new SolidColorBrush(Colors.Gray);
                CheckBox_ExposureTime.Foreground = new SolidColorBrush(Colors.Gray);
            }
            CheckBox_ExposureTime_Click(null, null);
        }

        private void GroupBox_Files_attribute(bool bActive)
        {
            if (bActive)
            {
                GroupBox_Files.IsEnabled = true;
                GroupBox_Files.Foreground = new SolidColorBrush(Colors.White);
                TEXT_SaveDirectoryt.Foreground = new SolidColorBrush(Colors.White);
                GroupBox_Attribute.Foreground = new SolidColorBrush(Colors.White);
                Radio_None.Foreground = new SolidColorBrush(Colors.White);
                Radio_Attribute1.Foreground = new SolidColorBrush(Colors.White);
                Radio_Attribute2.Foreground = new SolidColorBrush(Colors.White);
                Radio_Attribute3.Foreground = new SolidColorBrush(Colors.White);
                Lavel_FileIndex.Foreground = new SolidColorBrush(Colors.White);
                TEXT_FileIndex.Foreground = new SolidColorBrush(Colors.White);
                Lavel_FileName.Foreground = new SolidColorBrush(Colors.White);
                TEXT_FileName.Foreground = new SolidColorBrush(Colors.White);
                GroupBox_AutoSave_attribute(true);
            }
            else
            {
                GroupBox_Files.IsEnabled = false;
                GroupBox_Files.Foreground = new SolidColorBrush(Colors.Gray);
                TEXT_SaveDirectoryt.Foreground = new SolidColorBrush(Colors.Gray);
                GroupBox_Attribute.Foreground = new SolidColorBrush(Colors.Gray);
                Radio_None.Foreground = new SolidColorBrush(Colors.Gray);
                Radio_Attribute1.Foreground = new SolidColorBrush(Colors.Gray);
                Radio_Attribute2.Foreground = new SolidColorBrush(Colors.Gray);
                Radio_Attribute3.Foreground = new SolidColorBrush(Colors.Gray);
            //  Lavel_FileIndex.Foreground = new SolidColorBrush(Colors.Gray);
            //  TEXT_FileIndex.Foreground = new SolidColorBrush(Colors.Gray);
            //  Lavel_FileName.Foreground = new SolidColorBrush(Colors.Gray);
            //  TEXT_FileName.Foreground = new SolidColorBrush(Colors.Gray);
                GroupBox_AutoSave_attribute(false);
            }
        }

        private void GroupBox_AutoSave_attribute(bool bActive)
        {
            if (bActive)
            {
                GroupBox_AutoSave.IsEnabled = true;
                GroupBox_AutoSave.Foreground = new SolidColorBrush(Colors.White);
                CheckBox_AutoSave.Foreground = new SolidColorBrush(Colors.White);
            }
            else
            {
                GroupBox_AutoSave.IsEnabled = false;
                GroupBox_AutoSave.Foreground = new SolidColorBrush(Colors.Gray);
                CheckBox_AutoSave.Foreground = new SolidColorBrush(Colors.Gray);
            }
            CheckBox_AutoSave_Click(null, null);
        }

        private void GroupBox_EasyImage_Adjust3_attribute(bool bActive)
        {
            if (bActive)
            {
                label_EasyImage_TAdjust3.IsEnabled = true;
                label_EasyImage_TAdjust3.Foreground = new SolidColorBrush(Colors.White);
                label_EasyImage_Adjust3.IsEnabled = true;
                label_EasyImage_Adjust3.Foreground = new SolidColorBrush(Colors.White);
                sLider_EasyImage_Adjust3.IsEnabled = true;
            }
            else
            {
                label_EasyImage_TAdjust3.IsEnabled = false;
                label_EasyImage_TAdjust3.Foreground = new SolidColorBrush(Colors.Gray);
                label_EasyImage_Adjust3.IsEnabled = false;
                label_EasyImage_Adjust3.Foreground = new SolidColorBrush(Colors.Gray);
                sLider_EasyImage_Adjust3.IsEnabled = false;
            }
        }

        private void GroupBox_Open_eVision_attribute(bool bActive)
        {
            if (bActive)
            {
                TPreProcessingTime.Foreground = new SolidColorBrush(Colors.White);
                PreProcessingTime.Foreground = new SolidColorBrush(Colors.White);
                TMainProcessingTime.Foreground = new SolidColorBrush(Colors.White);
                MainProcessingTime.Foreground = new SolidColorBrush(Colors.White);
                GroupBox_EasyImage_CannyEdgeDetector.IsEnabled = true;
                GroupBox_EasyImage_CannyEdgeDetector.Foreground = new SolidColorBrush(Colors.White);

                GroupBox_UsingCore.IsEnabled = true;
                GroupBox_UsingCore.Foreground = new SolidColorBrush(Colors.White);
                label_TProcessCores.IsEnabled = true;
                label_TProcessCores.Foreground = new SolidColorBrush(Colors.White);
                label_ProcessCores.IsEnabled = true;
                label_ProcessCores.Foreground = new SolidColorBrush(Colors.White);
                sLider_ProcessCores.IsEnabled = true;
                sLider_ProcessCores.Foreground = new SolidColorBrush(Colors.White);

                GroupBox_Mode_Selection.IsEnabled = true;
                GroupBox_Mode_Selection.Foreground = new SolidColorBrush(Colors.White);
                RadioButton_Absolute.IsEnabled = true;
                RadioButton_Absolute.Foreground = new SolidColorBrush(Colors.White);
                RadioButton_Relative.IsEnabled = true;
                RadioButton_Relative.Foreground = new SolidColorBrush(Colors.White);
                label_EasyImage_TAdjust1.IsEnabled = true;
                label_EasyImage_TAdjust1.Foreground = new SolidColorBrush(Colors.White);
                sLider_EasyImage_Adjust1.IsEnabled = true;
                label_EasyImage_TAdjust2.IsEnabled = true;
                label_EasyImage_TAdjust2.Foreground = new SolidColorBrush(Colors.White);
                sLider_EasyImage_Adjust2.IsEnabled = true;
                GroupBox_EasyImage_Adjust3.IsEnabled = true;
                GroupBox_EasyImage_Adjust3.Foreground = new SolidColorBrush(Colors.White);
                CheckBox_EasyImage_Adjust3.IsEnabled = true;
                CheckBox_EasyImage_Adjust3.Foreground = new SolidColorBrush(Colors.White);
                if (CheckBox_EasyImage_Adjust3.IsChecked == true)
                    GroupBox_EasyImage_Adjust3_attribute(false);
                else
                    GroupBox_EasyImage_Adjust3_attribute(true);

                Text_CameraImage.Text = "Result Image";
            //  TDisplayprocessingTime.Foreground = new SolidColorBrush(Colors.White);
            //  DisplayprocessingTime.Foreground = new SolidColorBrush(Colors.White);
            }
            else
            {
                TPreProcessingTime.Foreground = new SolidColorBrush(Colors.Gray);
                PreProcessingTime.Text = "";
                PreProcessingTime.Foreground = new SolidColorBrush(Colors.Gray);
                TMainProcessingTime.Foreground = new SolidColorBrush(Colors.Gray);
                MainProcessingTime.Text = "";
                MainProcessingTime.Foreground = new SolidColorBrush(Colors.Gray);

                GroupBox_EasyImage_CannyEdgeDetector.IsEnabled = false;
                GroupBox_EasyImage_CannyEdgeDetector.Foreground = new SolidColorBrush(Colors.Gray);

                GroupBox_UsingCore.IsEnabled = false;
                GroupBox_UsingCore.Foreground = new SolidColorBrush(Colors.Gray);
                label_TProcessCores.IsEnabled = false;
                label_TProcessCores.Foreground = new SolidColorBrush(Colors.Gray);
                label_ProcessCores.IsEnabled = false;
                label_ProcessCores.Foreground = new SolidColorBrush(Colors.Gray);
                sLider_ProcessCores.IsEnabled = false;
                sLider_ProcessCores.Foreground = new SolidColorBrush(Colors.Gray);

                GroupBox_Mode_Selection.IsEnabled = false;
                GroupBox_Mode_Selection.Foreground = new SolidColorBrush(Colors.Gray);
                RadioButton_Absolute.IsEnabled = false;
                RadioButton_Absolute.Foreground = new SolidColorBrush(Colors.Gray);
                RadioButton_Relative.IsEnabled = false;
                RadioButton_Relative.Foreground = new SolidColorBrush(Colors.Gray);
                label_EasyImage_TAdjust1.IsEnabled = true;
                label_EasyImage_TAdjust1.Foreground = new SolidColorBrush(Colors.Gray);
                sLider_EasyImage_Adjust1.IsEnabled = false;
                label_EasyImage_TAdjust2.IsEnabled = false;
                label_EasyImage_TAdjust2.Foreground = new SolidColorBrush(Colors.Gray);
                sLider_EasyImage_Adjust2.IsEnabled = false;
                GroupBox_EasyImage_Adjust3.IsEnabled = false;
                GroupBox_EasyImage_Adjust3.Foreground = new SolidColorBrush(Colors.Gray);
                CheckBox_EasyImage_Adjust3.IsEnabled = false;
                CheckBox_EasyImage_Adjust3.Foreground = new SolidColorBrush(Colors.Gray);
                GroupBox_EasyImage_Adjust3_attribute(false);

                Text_CameraImage.Text = "Source Image";
            //  TDisplayprocessingTime.Foreground = new SolidColorBrush(Colors.Gray);
            //  DisplayprocessingTime.Text = "";
            //  DisplayprocessingTime.Foreground = new SolidColorBrush(Colors.Gray);
            }
        }

        private void DisposeUnmanagedResources()
        {
            myCamera.Close();
            Easy.Terminate();
        }

        private string PreparingFileName()
        {
            // This attribute is for deep learning image collection.
            string sAttribute = "";
            if (Radio_Attribute3.IsChecked == true)
                sAttribute = sSaveFile_Attribute3;
            else if (Radio_Attribute2.IsChecked == true)
                sAttribute = sSaveFile_Attribute2;
            else if (Radio_Attribute1.IsChecked == true)
                sAttribute = sSaveFile_Attribute1;
            string sSaveFileName = sFileName + "_" + sAttribute + (mFileNumber++).ToString("D3") + sFileType;
            return sSaveFileName;
        }

        private void SaveImage()
        {
            sFileDirectory = TEXT_SaveDirectoryv.Text;
            if (sFileDirectory.Substring(sFileDirectory.Length - 1, 1) != "\\")
            {
                sFileDirectory = sFileDirectory + "\\";
                TEXT_SaveDirectoryv.Text = sFileDirectory;
            }

            if (!System.IO.Directory.Exists(sFileDirectory))
            {
                System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(sFileDirectory);
                di.Create();
            }
            string sSaveFileName = PreparingFileName();
            TEXT_FileName.Text = sSaveFileName;
            sSaveFileName = sFileDirectory + sSaveFileName;
            using (FileStream stream = new FileStream(sSaveFileName, FileMode.Create, FileAccess.Write))
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                if (CheckBox_OpeneVision_Enable.IsChecked == true)
                    encoder.Frames.Add(BitmapFrame.Create(imageBitmapResult));
                else
                    encoder.Frames.Add(BitmapFrame.Create(myCamera.imageBitmap));
                encoder.Save(stream);
            }
            TEXT_FileIndex.Text = mFileNumber.ToString();
        }

        private void GenerateAutoSaveTimming(object sender, EventArgs e)
        {
            if (bAcquisition)
            {
                Button_Operation(null, null);
                SaveImage();
                Button_Operation(null, null);
            }
        }

        private void Open_eVision_Process()
        {
            if (myCamera.imageBitmap == null)
                return;
            if (!bProcessing)
            {
                bProcessing = true;
                ProcessingTime.Reset();
                ProcessingTime.Start();
                try
                {
                    if (RadioButton_Relative.IsChecked == true)
                        MycannyEdgeDetector.ThresholdingMode = ECannyThresholdingMode.Relative;
                    else
                        MycannyEdgeDetector.ThresholdingMode = ECannyThresholdingMode.Absolute;
                    MycannyEdgeDetector.HighThreshold = m_HighThreshold;
                    MycannyEdgeDetector.LowThreshold = m_LowThreshold;
                    MycannyEdgeDetector.SmoothingScale = m_SmoothingScale;
                //  if (myCamera.m_Image_Format.Contains("Mono"))
                //  {
                        imageBitmapResult = new WriteableBitmap((int)m_Image_Width, (int)m_Image_Height, 96, 96, System.Windows.Media.PixelFormats.Gray8, null);
                        IntPtr pBackBufferS = imageBitmapResult.BackBuffer;
                        imageBitmapResult.Lock();
                        EImageBW8 newImageBW8 = new EImageBW8((int)m_Image_Width, (int)m_Image_Height);
                        newImageBW8.SetImagePtr((int)m_Image_Width, (int)m_Image_Height, pBackBufferS);
                        MycannyEdgeDetector.Apply(myCamera.imageBW8, newImageBW8);
                        imageBitmapResult.AddDirtyRect(new Int32Rect(0, 0, (int)m_Image_Width, (int)m_Image_Height));
                        imageBitmapResult.Unlock();
                        CameraImage.Source = imageBitmapResult;
                //  }
                //  else
                //  {
                        // cannyEdgeDetector does not support color image space.
                //  }
                }
                catch
                {
                    //
                }
                tsMainProcessingTime = ProcessingTime.Elapsed;
                bProcessing = false;
            }
        }

        private void GettingImage(object sender, EventArgs e)
        {
            if (!bCapturing)
            {
                bCapturing = true;
                myCamera.get_Image();
                if (CheckBox_OpeneVision_Enable.IsChecked == true)
                {
                    switch (sThreadState)
                    {
                        case State.Ready:
                            sThreadState = State.Processing;
                            Open_eVision_Process();
                            tsPreProcessingAverage = (myCamera.tsPreProcessingTime + 3 * tsPreProcessingAverage) / 4;
                            tsMainProcessingAverage = (tsMainProcessingTime + 3 * tsMainProcessingAverage) / 4;
                            if (myCamera.tsPreProcessingTime.Milliseconds == 0)
                                PreProcessingTime.Text = myCamera.tsPreProcessingTime.Microseconds.ToString() + "us";
                            else
                                PreProcessingTime.Text = myCamera.tsPreProcessingTime.Milliseconds.ToString() + "ms";
                            if (tsPreProcessingAverage.Milliseconds == 0)
                                PreProcessingAverage.Text = tsPreProcessingAverage.Microseconds.ToString() + "us";
                            else
                                PreProcessingAverage.Text = tsPreProcessingAverage.Milliseconds.ToString() + "ms";
                            if (tsMainProcessingTime.Milliseconds == 0)
                                MainProcessingTime.Text = tsMainProcessingTime.Microseconds.ToString() + "us";
                            else
                                MainProcessingTime.Text = tsMainProcessingTime.Milliseconds.ToString() + "ms";
                            if (tsMainProcessingAverage.Milliseconds == 0)
                                MainProcessingAverage.Text = tsMainProcessingAverage.Microseconds.ToString() + "us";
                            else
                                MainProcessingAverage.Text = tsMainProcessingAverage.Milliseconds.ToString() + "ms";
                            sThreadState = State.Ready;
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    CameraImage.Source = myCamera.imageBitmap;
                    PreProcessingTime.Text = "_";
                    MainProcessingTime.Text = "_";
                }
                FrameRate.Text = myCamera.m_Frame_Rate.ToString("F1");
                RejectFrame.Text = myCamera.m_DropFrameCount.ToString();
                bCapturing = false;
            }
        }

        private void Button_Operation(object sender, RoutedEventArgs e)
        {
            if (!bAcquisition)
            {
                CheckBox_AutoSave_Click(null, null);
                GroupBox_AutoSave_attribute(false);
                myCamera.StartStream();
                sLider_IntervalTime.IsEnabled = false;
                dispatcherTimerGettingImage.Start();
                if (CheckBox_AutoSave.IsChecked == true)
                    dispatcherTimerAutoImageSave.Start();
                tButton_Operation.Content = "Stop";
                bAcquisition = true;
            }
            else if (bAcquisition)
            {
                bAcquisition = false;
                tButton_Operation.Content = "Start";
                if (CheckBox_AutoSave.IsChecked == true)
                    dispatcherTimerAutoImageSave.Stop();
                dispatcherTimerGettingImage.Stop();
                myCamera.StopStream();
                sLider_IntervalTime.IsEnabled = true;
                GroupBox_AutoSave_attribute(true);
                FrameRate.Text = "-";
            }
        }

        private void CheckBox_ExposureTime_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox_ExposureTime.IsChecked == true)
            {
                label_Exposure.IsEnabled = true;
                label_Exposure.Foreground = new SolidColorBrush(Colors.White);
                label_ExposureTime.IsEnabled = true;
                label_ExposureTime.Foreground = new SolidColorBrush(Colors.White);
                sLider_ExposureTime.IsEnabled = true;
            }
            else
            {
                label_Exposure.IsEnabled = false;
                label_Exposure.Foreground = new SolidColorBrush(Colors.Gray);
                label_ExposureTime.IsEnabled = false;
                label_ExposureTime.Foreground = new SolidColorBrush(Colors.Gray);
                sLider_ExposureTime.IsEnabled = false;
            }
        }

        private void sLider_ExposureTime_MouseEnter(object sender, MouseEventArgs e)
        {
            bsLider_Exposure = true;
        }

        private void sLider_ExposureTime_MouseLeave(object sender, MouseEventArgs e)
        {
            bsLider_Exposure = false;
        }

        private void sLider_ExposureTime_MouseMove(object sender, MouseEventArgs e)
        {
            if (bsLider_Exposure && bInitilizeDone)
            {
                int dExposure = (int)sLider_ExposureTime.Value;
                if (dExposure < Convert.ToDouble(myCamera.GetParam("ExposureTime.Min")))
                    dExposure = (int)Convert.ToDouble(myCamera.GetParam("ExposureTime.Min"));
                else if (Convert.ToDouble(myCamera.GetParam("ExposureTime.Max")) < dExposure)
                    dExposure = (int)Convert.ToDouble(myCamera.GetParam("ExposureTime.Max"));
                myCamera.SetParam("ExposureTime", dExposure.ToString());
                label_ExposureTime.Text = dExposure.ToString();
            }
        }

        private void TEXT_FileIndex_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Sub_Dialog_Index dlg = new Sub_Dialog_Index();
            dlg.iIndex = mFileNumber;
            dlg.ShowDialog();
            mFileNumber = dlg.iIndex;
            TEXT_FileIndex.Text = mFileNumber.ToString();
            TEXT_FileName.Text = PreparingFileName();
            mFileNumber--;
        }

        private void Button_SaveImage(object sender, RoutedEventArgs e)
        {
            bool bAcq = false;
            if (bAcquisition)
            {
                Button_Operation(null, null);
                bAcq = true;
            }
            SaveImage();
            if (bAcq)
                Button_Operation(null, null);
            else
            {
                tButton_SaveImage.IsEnabled = false;
                tButton_SaveImage.Foreground = new SolidColorBrush(Colors.LightGray);
            }
        }

        private void CheckBox_AutoSave_Click(object sender, RoutedEventArgs e)
        {
            sFileDirectory = TEXT_SaveDirectoryv.Text;
            if (CheckBox_AutoSave.IsChecked == true)
            {
                label_Interval.IsEnabled = true;
                label_Interval.Foreground = new SolidColorBrush(Colors.White);
                label_IntervalValue.IsEnabled = true;
                label_IntervalValue.Foreground = new SolidColorBrush(Colors.White);
                sLider_IntervalTime.IsEnabled = true;
                tButton_SaveImage.IsEnabled = false;
                tButton_SaveImage.Foreground = new SolidColorBrush(Colors.LightGray);
            }
            else
            {
                label_Interval.IsEnabled = false;
                label_Interval.Foreground = new SolidColorBrush(Colors.Gray);
                label_IntervalValue.IsEnabled = false;
                label_IntervalValue.Foreground = new SolidColorBrush(Colors.Gray);
                sLider_IntervalTime.IsEnabled = false;
                tButton_SaveImage.IsEnabled = true;
                tButton_SaveImage.Foreground = new SolidColorBrush(Colors.Black);
            }
        }

        private void sLider_IntervalTime_MouseEnter(object sender, MouseEventArgs e)
        {
            bsLider_Interval = true;
        }

        private void sLider_IntervalTime_MouseLeave(object sender, MouseEventArgs e)
        {
            bsLider_Interval = false;
        }

        private void sLider_Interval_Set()
        {
            uint mInterval = (uint)sLider_IntervalTime.Value;
            if (mInterval > 0)
            {
                label_IntervalValue.Text = mInterval.ToString();
                dispatcherTimerAutoImageSave.Interval = TimeSpan.FromMilliseconds((double)(sLider_IntervalTime.Value * 1000));
            }
        }

        private void sLider_Interval_MouseMove(object sender, MouseEventArgs e)
        {
            if (bsLider_Interval && (CheckBox_AutoSave.IsChecked == true) && bInitilizeDone)
                sLider_Interval_Set();
        }

        private void CheckBox_OpeneVision_Enable_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox_OpeneVision_Enable.IsChecked == true)
            {
                GroupBox_Open_eVision_attribute(true);
            }
            else
            {
                GroupBox_Open_eVision_attribute(false);
            }
        }

        private void sLider_ProcessCores_MouseEnter(object sender, MouseEventArgs e)
        {
            bsLider_ProcessCores = true;
        }

        private void sLider_ProcessCores_MouseLeave(object sender, MouseEventArgs e)
        {
            bsLider_ProcessCores = false;
        }

        private void sLider_ProcessCores_MouseMove(object sender, MouseEventArgs e)
        {
            if (bsLider_ProcessCores && bInitilizeDone)
            {
                try
                {
                    int m_Cores = (int)sLider_ProcessCores.Value;
                    Easy.MaxNumberOfProcessingThreads = m_Cores;
                    label_ProcessCores.Text = m_Cores.ToString();
                }
                catch
                { }
            }
        }

        private void sLider_EasyImage_Adjust1_MouseEnter(object sender, MouseEventArgs e)
        {
            bsLider_EasyImage_Adjust1 = true;
        }

        private void sLider_EasyImage_Adjust1_MouseLeave(object sender, MouseEventArgs e)
        {
            bsLider_EasyImage_Adjust1 = false;
        }

        private void sLider_EasyImage_Adjust1_MouseMove(object sender, MouseEventArgs e)
        {
            if (bsLider_EasyImage_Adjust1 && bInitilizeDone)
            {
                m_HighThreshold = (float)sLider_EasyImage_Adjust1.Value;
                label_EasyImage_Adjust1.Text = m_HighThreshold.ToString("F2");
            }
        }

        private void sLider_EasyImage_Adjust2_MouseEnter(object sender, MouseEventArgs e)
        {
            bsLider_EasyImage_Adjust2 = true;
        }

        private void sLider_EasyImage_Adjust2_MouseLeave(object sender, MouseEventArgs e)
        {
            bsLider_EasyImage_Adjust2 = false;
        }

        private void sLider_EasyImage_Adjust2_MouseMove(object sender, MouseEventArgs e)
        {
            if (bsLider_EasyImage_Adjust2 && bInitilizeDone)
            {
                m_LowThreshold = (float)sLider_EasyImage_Adjust2.Value;
                label_EasyImage_Adjust2.Text = m_LowThreshold.ToString("F2");
            }
        }

        private void CheckBox_EasyImage_Adjust3_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox_EasyImage_Adjust3.IsChecked == true)
            {
                GroupBox_EasyImage_Adjust3_attribute(true);
            }
            else
            {
                GroupBox_EasyImage_Adjust3_attribute(false);
                sLider_EasyImage_Adjust3.Value = 0;
                m_SmoothingScale = 0;
                label_EasyImage_Adjust3.Text = "0";
            }
        }

        private void sLider_EasyImage_Adjust3_MouseEnter(object sender, MouseEventArgs e)
        {
            bsLider_EasyImage_Adjust3 = true;
        }

        private void sLider_EasyImage_Adjust3_MouseLeave(object sender, MouseEventArgs e)
        {
            bsLider_EasyImage_Adjust3 = false;
        }

        private void sLider_EasyImage_Adjust3_MouseMove(object sender, MouseEventArgs e)
        {
            if (bsLider_EasyImage_Adjust3 && bInitilizeDone)
            {
                m_SmoothingScale = (float)sLider_EasyImage_Adjust3.Value;
                label_EasyImage_Adjust3.Text = m_SmoothingScale.ToString("F2");
            }
        }

        private void Button_Exit(object sender, RoutedEventArgs e)
        {
            DisposeUnmanagedResources();
            Close();
        }
    }
}