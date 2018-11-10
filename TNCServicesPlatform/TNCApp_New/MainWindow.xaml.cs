﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using System.Collections;
using System.Deployment.Application;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using CNTK;
using CNTKImageProcessing;
using Microsoft.Cognitive.CustomVision.Training.Models;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Microsoft.WindowsAzure.Storage.Blob;
using TNCAnimalLabelWebAPI;
using TNCAnimalLabelWebAPI.Models;
using TNCApp_New.CNTK;
using TNCApp_New.Models;
using Image = System.Drawing.Image;
using ListBox = System.Windows.Controls.ListBox;
using TNCApp_New.Utilities;
using TNCServicesPlatform.StorageAPI.Models;

namespace TNCApp_New
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static ManualResetEvent mre = new ManualResetEvent(false);
        public bool StateLocal { get; set; }
        public int ConfidenceRate { get; set; }
        public string RootFolder { get; set; }
        public string ConfirmFolder { get; set; }
        public string DataVisFolder { get; set; }
        public string BrowseFolder { get; set; }
        Function modelFunc { get; set; }
        public string RootProcessFolder { get; set; }
        public string GenerateFolder { get; set; }
        public List<string> CameraLocations { get; set; }
        public List<string> CameraNumbers { get; set; }
        public List<string> CorrectedTime { get; set; }
        public int IndexCamera { get; set; }
        public bool SinorMul { get; set; }
        public int DirNum { get; set; }
        public bool IsContinue { get; set; }
        public bool UploadOnly { get; set; }
        public bool isThreshhold { get; set; }
        public string AnimalLocationTextFile { get; set; }
        public List<Prediction> LocalSorted { get; set; }
        public bool PredictionOnly { get; set; }



        public MainWindow()
        {
            
            InitializeComponent();
            ConfigGrid.Visibility = Visibility.Collapsed;
            //ConfidenceBarText.Text = "Rate: " + ConfidenceBar.Value + "%";
            IndexCamera = 0;
            CameraLocations = new List<string>();
            CameraNumbers = new List<string>();
            CorrectedTime = new List<string>();
            this.UploadingBar.Value = 0;
            this.OnlineCheckBox.IsChecked = true;
            this.StateLocal = false;
            this.ConfidenceRate = 100;
            isThreshhold = false;
            PredictionOnly = true;
            this.PredictionOnlyCheckBox.IsChecked = true;
            this.FolderProgressBox.Text = "click predict to select image folder, confirm/report to check results";


            DirNum = 1;
            IsContinue = false;
            //for deployment
            string domainBaseDirectory =  ApplicationDeployment.IsNetworkDeployed ? ApplicationDeployment.CurrentDeployment.DataDirectory: AppDomain.CurrentDomain.BaseDirectory;
            string modelFilePath = Directory.GetFiles(domainBaseDirectory, "TNC_ResNet18_ImageNet_CNTK.model",SearchOption.AllDirectories)[0]; ; //Path.Combine(domainBaseDirectory, @"CNTK\Models\TNC_ResNet18_ImageNet_CNTK.model");
                if (!File.Exists(modelFilePath))
                {
                    throw new FileNotFoundException(modelFilePath, string.Format("Error: The model '{0}' does not exist. Please follow instructions in README.md in <CNTK>/Examples/Image/Classification/ResNet to create the model.", modelFilePath));
                }

            if (!PredictionOnly) {
                try
                {
                    DeviceDescriptor device = DeviceDescriptor.CPUDevice;// DeviceDescriptor.CPUDevice;
                    modelFunc = Function.Load(modelFilePath, device);
                }
                catch (Exception e)
                {
                    richTextBox1.Text = e.Message;
                    ;
                    //throw;
                }
            }

                
                //modelFunc = Function.Load(modelFilePath, device);
            RootFolder= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "TNC");
            var locationtextfile = "animallocation.txt";
             AnimalLocationTextFile = Path.Combine(RootFolder, locationtextfile); 
            ConfirmFolder = RootFolder;
            if (PredictionOnly) {
                ConfirmFolder = Path.Combine(RootFolder, "demo");
            }
            //DataVisFolder = Path.Combine(RootFolder, "datavis");
            //BrowseFolder = Path.Combine(RootFolder, "browse");
            //GenerateFolder = Path.Combine(RootFolder, "Generate");
            System.IO.Directory.CreateDirectory(ConfirmFolder);
           // System.IO.Directory.CreateDirectory(DataVisFolder);
           // System.IO.Directory.CreateDirectory(BrowseFolder);
            //System.IO.Directory.CreateDirectory(GenerateFolder);
            if (!System.IO.File.Exists(AnimalLocationTextFile))
            {
                System.IO.File.Create(AnimalLocationTextFile);
            }

        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
        void UploadLocation(string locationjson)
        {
            try
            {
                JsonConvert.DeserializeObject<List<AnimalLocation>>(locationjson);
            }
            catch (Exception e)
            {
                this.richTextBox1.Text = "location information invalid, check your location txt file in TNC folder on desktop";
                return;
            }
            try
            {
                var client = new HttpClient();

                // 1. Upload meta data to Cosmos DB
                string uploadUrl = "http://localhost:55464/api/storage/location";
                byte[] byteData = Encoding.UTF8.GetBytes(locationjson);
                HttpResponseMessage response;
                var debug = UploadOnly;
                using (var content = new ByteArrayContent(byteData))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    this.richTextBox1.Text = ("Sending location information");
                    //this.UploadingBar.Value += 33;

                    response = client.PutAsync(uploadUrl, content).Result;
                    //response = client.PostAsync(uploadUrl, content).Result;
                }
                UploadOnly = debug;
                string responseStr = response.Content.ReadAsStringAsync().Result;
                
                try
                {
                    JsonConvert.DeserializeObject<List<AnimalLocation>>(responseStr);
                }
                catch (Exception e)
                {
                    this.richTextBox1.Text = e.ToString();
                    return;
                }
                File.WriteAllText(AnimalLocationTextFile, responseStr);
                return ;
            }
            catch (Exception ex)
            {
                this.richTextBox1.Text = ex.ToString();
                return;
            }
        }
        //using the image path, upload the image to cosmos and storage account
        AnimalImage UploadImage(string imagePath , AnimalImage animalImage)
        {
            
            try
            {
                //this.UploadingBar.Value = 0;
                var client = new HttpClient();
                animalImage.ImageName = Path.GetFileName(imagePath);

                // 1. Upload meta data to Cosmos DB
                string uploadUrl = "http://tncapi.azurewebsites.net/api/storage/";
                string imageJson = JsonConvert.SerializeObject(animalImage);
                byte[] byteData = Encoding.UTF8.GetBytes(imageJson);
                HttpResponseMessage response;
                var debug = UploadOnly;
                using (var content = new ByteArrayContent(byteData))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    this.richTextBox1.Text = ("Sending image info to Cosmos DataBase");
                    //this.UploadingBar.Value += 33;
                    
                   response = client.PutAsync(uploadUrl, content).Result;
                    //response = client.PostAsync(uploadUrl, content).Result;
                }

                UploadOnly = debug;
                string responseStr = response.Content.ReadAsStringAsync().Result;
                Console.WriteLine(responseStr);
                AnimalImage imageResponse = JsonConvert.DeserializeObject<AnimalImage>(responseStr);

                // 2. uppload image self to blob storage
                byte[] blobContent = File.ReadAllBytes(imagePath);
                CloudBlockBlob blob = new CloudBlockBlob(new Uri(imageResponse.UploadBlobSASUrl));
                MemoryStream msWrite = new MemoryStream(blobContent);
                msWrite.Position = 0;

                using (msWrite)
                {
                    this.richTextBox1.Text = ("Uploading image data to Storage Colletion");
                    blob.UploadFromStreamAsync(msWrite).Wait();
                    //this.UploadingBar.Value += 33;
                }

                return imageResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw ex;
            }
        }

        async Task<ImagePredictionResultModel> MakePredictionRequestCNTK(string imageUrl)
        {
            try
            {
                var client = new HttpClient();
                var uri = "http://tncapi.azurewebsites.net/api/prediction/cntknet";

                byte[] byteData = Encoding.UTF8.GetBytes("\"" + imageUrl + "\"");
                HttpResponseMessage response;

                using (var content = new ByteArrayContent(byteData))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    response = await client.PostAsync(uri, content);
                    this.UploadingBar.Value += 33;
                }

                string res = response.Content.ReadAsStringAsync().Result;
                var resObj = JsonConvert.DeserializeObject<ImagePredictionResultModel>(res);
                return resObj;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw ex;
            }
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            this.ListV.ItemsSource = new List<String>();
            this.UploadingBar.Value = 0;
            this.richTextBox1.Text="";
            this.pictureBox1.Source = new BitmapImage();
            this.ConfirmButton.Visibility = Visibility.Hidden;
            this.ConfirmTextBox.Visibility = Visibility.Hidden;

        }
        //start the entier prediction process
        //turn to different operation based on different file type loaded

        private  void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            UploadOnly = false;

            StartPrediction1(singleProcess);


        }

        void AllowUIToUpdate()
        {

            DispatcherFrame frame = new DispatcherFrame();

            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new DispatcherOperationCallback(delegate (object parameter)

            {

                frame.Continue = false;

                return null;

            }), null);

            Dispatcher.PushFrame(frame);

        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
           
        }


        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            this.StateLocal = false;
            this.TextLineBox.Text = "Online";
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (PredictionOnly) {
                this.richTextBox1.Text = "sorry demo version only support online mode";
                return;
            }
            this.StateLocal = true;
            this.TextLineBox.Text = "Offline";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ListV_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            
        }

        private void ListV_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            
            mre.Set();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            mre.Set();
        }

        void confrim_photos()
        {
            if (PredictionOnly) {
                Task mytask2 = Task.Run(() => {
                    GenerateCSV1(new List<string>
            {
               Path.Combine(ConfirmFolder,".csv")
            });
                });
                return;
            }
            OpenFileDialog confirmdlg = new OpenFileDialog();
            confirmdlg.DefaultExt = ".csv";
            confirmdlg.ShowDialog();

            var confirmpath = confirmdlg.FileName;
            if (!File.Exists(confirmpath))
            {
                richTextBox1.Text = "not vaild file";
                return;
            }

            //switch (Path.GetExtension(confirmpath))
            //{
            //    case ".tnc":
            //        using (Stream stream = File.Open(confirmpath, FileMode.Open))
            //        {
            //            var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            //            var nice = (Tuple<string, string, List<ConfirmPredictionModel>, string,DateTime>)binaryFormatter.Deserialize(stream);
            //            var cameraName = nice.Item1;
            //            var folderName = nice.Item2;
            //            var confirmPredictions = nice.Item3;
            //            var rootdir = nice.Item4;
            //            var correctTime = nice.Item5;
            //            Task mytask1 = Task.Run(() => { GenerateCSV1(cameraName, folderName, confirmPredictions, rootdir, isThreshhold, correctTime,confirmpath); });
            //        }

            Task mytask1 = Task.Run(() => { GenerateCSV1(new List<string>
            {
                confirmpath
            }); });
            //        break;

            //}

            //File.Move(confirmpath,Path.Combine(BrowseFolder,Path.GetDirectoryName(confirmpath)));
            this.BoundingBox.Visibility = Visibility.Hidden;

        }
        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            this.isThreshhold = true;
            this.FolderProgressBox.Text = "";
            this.UploadingBar.Value = 0;
            confrim_photos();
            //newconfirm_photos();
            

        }
        private void get_reports(string path)
        {
            if (!File.Exists(path))
            {
                richTextBox1.Text = "not vaild file";
                return;
            }
            int i = 0;
            IDictionary<string, int> dict = new Dictionary<string, int>();
            IDictionary<string, int> dictratio = new Dictionary<string, int>();
            dictratio.Add("correct", 0);
            dictratio.Add("wrong", 0);
            IDictionary<string, int> dictAttribute = new Dictionary<string, int>();
            var reader = new StreamReader(path, Encoding.Default);
            var file = reader.ReadToEnd();
            reader.Close();
            var lines = file.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            var attributes = lines[0].Split(',');
            attributes[attributes.Length - 1] = attributes[attributes.Length - 1].Replace("\r", "");
            foreach (var attribute in attributes)
            {
                dictAttribute.Add(attribute, i);
                i++;
            }
            i = 0;
            foreach (var line in lines)
            {

                if (i == 0)
                {
                    i++;
                    continue;
                }

                var elements = line.Split(',');
                if (elements[dictAttribute["文件格式"]] != "JPG")
                {
                    continue;
                }
                var speciesName = elements[dictAttribute["物种名称"]];
                var speciesNameai = elements[dictAttribute["aitag"]];
                if (speciesName == "")
                {
                    speciesName = "unassigned";
                }
                if (dict.ContainsKey(speciesName))
                {
                    dict[speciesName]++;
                }
                else
                {
                    dict.Add(speciesName, 1);
                }

                if (speciesName == speciesNameai)
                {
                    dictratio["correct"]++;
                }
                else
                {
                    dictratio["wrong"]++;
                }

            }

            DataVisualization div = new DataVisualization(dict, dictratio);
            // Configure the dialog box
            div.Owner = this;
            // Open the dialog box modally 
            div.Show();
        }

        private void Reports_Click(object sender, RoutedEventArgs e)
        {
            if (PredictionOnly) {
                get_reports(Path.Combine(ConfirmFolder, ".csv"));
                return;
            }
            this.FolderProgressBox.Text = "";
            OpenFileDialog datadlg = new OpenFileDialog();
            datadlg.InitialDirectory = ConfirmFolder;
            datadlg.DefaultExt = ".csv";
            datadlg.ShowDialog();
            get_reports(datadlg.FileName);
        }

        async void singleProcess(List<string> imagePaths)
        {
            if (imagePaths.Count == 0)
            {
                this.richTextBox1.Text = "invalid input";
                return;
            }

            var Pathname = imagePaths[0];
            ;
/*            if (Directory.GetFiles(Path.GetDirectoryName(Pathname), "done.txt", SearchOption.TopDirectoryOnly).Length !=
                0)
            {
                if(IsContinue == true)
                return;
                else
            }*/

            //List<String> items = new List<String>();
            int i = 0;
            int totalFilesNum = 0;
            ImagePredictionResultModel result;
            var csv = new StringBuilder();
            var ConfirmPredictions = new List<ConfirmPredictionModel>();
            this.richTextBox1.Text = ("Processing...");
            AllowUIToUpdate();
            string cameraNumber;
            string folderName;
            string positionNumber;
            DateTime cortime = new DateTime();
            List<AnimalLocation> animalLocations=new List<AnimalLocation>(); 
            if (!File.Exists(Pathname))
            {
                this.richTextBox1.Text = "invlid file chosen";
                return;
            }

            if (Path.GetExtension(Pathname) != ".csv")
            {
                if (SinorMul)
                {


                    
                    Window1 dlg = new Window1();
                    //Ask for camera information
                    // Configure the dialog box
                    dlg.Owner = this;
                    // Open the dialog box modally 
                    dlg.ShowDialog();
                    dlg.Top = this.Top + 20;
                    cameraNumber = dlg.CameraNumber.Text;
                    folderName = dlg.CameraLocation.Text;
                    positionNumber = dlg.CameraLocation.Text;
                    if (!string.IsNullOrEmpty(dlg.CorectedTime.Text))
                    {
                        cortime = Convert.ToDateTime(dlg.CorectedTime.Text);
                    }
                    else
                    {
                        cortime = File.GetLastWriteTime(imagePaths[0]);
                    }

                }
                else
                {
                    if (PredictionOnlyCheckBox.IsChecked == true)
                    {
                        cameraNumber = "";
                        folderName = "";
                        positionNumber = "";
                        cortime = File.GetLastWriteTime(imagePaths[0]);
                    }
                    else {
                        cameraNumber = CameraNumbers[IndexCamera];
                        folderName = CameraLocations[IndexCamera];
                        positionNumber = CameraLocations[IndexCamera];
                        if (!string.IsNullOrEmpty(CorrectedTime[IndexCamera])) cortime = Convert.ToDateTime(CorrectedTime[IndexCamera]);
                        else
                        {
                            cortime = File.GetLastWriteTime(imagePaths[0]);
                        }
                    }

                    IndexCamera++;
                }

                totalFilesNum = imagePaths.Count;
            }

            else if (Path.GetExtension(Pathname) == ".csv")
            {
                processCSV(Pathname);
                if (UploadOnly)
                {
                    GenerateCSV1(new List<string>()
                    {
                        Pathname
                    } );
                    return;
                }
                imagePaths = new List<string>();
                string file;
                try
                {
                    file = new StreamReader(Pathname, Encoding.Default).ReadToEnd();
                }
                catch (Exception e)
                {
                    this.richTextBox1.Text = "could not open the csv file, because it is opened else where";
                    return;
                }
                
                var lines = file.Split('\n');
                var values = lines[1].Split(',');
                positionNumber = values[3];
                cameraNumber = values[4];
                int j = 0;
                var imagepath = Pathname;
                var d = Directory.GetFiles(Path.GetDirectoryName(imagepath), "*.JPG", SearchOption.AllDirectories);
                foreach (var line in lines)
                {
                    if (j == 0 || line == "")
                    {
                        j++;
                        continue;
                    }

                    values = line.Split(',');
                    var fileName = values[0];
                    var extension = values[2];
                    //assume the file is in a location

                    imagePaths.Add(Path.GetDirectoryName(d[0]) + $"\\{fileName}.{extension}");
                    j++;
                }

                totalFilesNum = imagePaths.Count;
                cortime = File.GetLastWriteTime(imagePaths[0]);

                ;
            }
            else
            {
                positionNumber = "";
                cameraNumber = "";
            }
            if(!PredictionOnly)
            UpdateAnimalLocation(positionNumber);

        List<int>indexs = new List<int>();
        foreach (String imagePath1 in imagePaths)
            {
                if (!File.Exists(imagePath1))
                {

                }
                else if (Path.GetExtension(imagePath1) == ".jpg" || Path.GetExtension(imagePath1) == ".JPG")
                {
                    this.pictureBox1.Source = new BitmapImage(new Uri(imagePath1));
                    //this.pictureBox1.Source = new BitmapImage(new Uri(imagePath, UriKind.RelativeOrAbsolute));
                    if (this.StateLocal != true)
                    {
                        AnimalImage emptyimage = new AnimalImage();
                        var image =  UploadImage(imagePath1, emptyimage);
                        //this.richTextBox1.Text = image.UploadBlobSASUrl;
                        // 2. image classification
                        if (UploadOnly)
                        {
                            i++;
                            this.UploadingBar.Value = Math.Round((double)i / (totalFilesNum) * 100);
                            continue;
                        }
                        string imageUrl = $"https://tncstorage4test.blob.core.windows.net/animalimages/{image.ImageBlob}";
                        this.richTextBox1.Text = ("Waiting for prediction result...");
                        result = await MakePredictionRequestCNTK(imageUrl);
                    }

                  
                    else
                    {
                        try
                        {
                            result = LocalPrediction.EvaluateCustomDNN(imagePath1, modelFunc);
                        }
                        catch (Exception e)
                        {
                            richTextBox1.Text = e.ToString();
                            throw;
                        }
                       
                    }
                    ConfirmPredictions.Add(new ConfirmPredictionModel()
                    {
                        IsPhoto = true,
                        FilePath = imagePath1,
                        Predictions = result.Predictions
                    });
                    indexs.Add(i);
                }
                else
                {
                    ConfirmPredictions.Add(new ConfirmPredictionModel()
                    {
                        IsPhoto = false,
                        FilePath = imagePath1
                    });
                    indexs.Add(i);
                }
                
                i++;
                this.UploadingBar.Value = Math.Round((double)i / (totalFilesNum) * 100);
                AllowUIToUpdate();

            }
            if(UploadOnly) return;
/*            var newdir = processDirctory(ConfirmFolder,Pathname, RootProcessFolder);
            var path = Path.Combine(newdir, positionNumber + ".tnc");
            bool append = false;
            using (Stream stream = File.Open(path, append ? FileMode.Append : FileMode.Create))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(stream, new Tuple<string, string, List<ConfirmPredictionModel>,string,DateTime>(cameraNumber, positionNumber, ConfirmPredictions,RootProcessFolder, cortime));
            }

            this.UploadingBar.Value = 100;
            this.richTextBox1.Text = ("Done");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Pathname), "done.txt"), "", Encoding.Default);*/



            i = 1;
            int workDays = 1;
            string file1;
            try
            {
                 file1 = new StreamReader(Pathname, Encoding.Default).ReadToEnd();
            }
            catch (Exception e)
            {
                richTextBox1.Text = "could not open the file, becasue it is opend else where";
                return;
            }
            
            var lines1 = file1.Split("\r\n".ToCharArray(),StringSplitOptions.RemoveEmptyEntries);
            var lastWorkDay = cortime;
            IDictionary<string, int> dict = new Dictionary<string, int>();
            //var csv = new StringBuilder();
            List<String> items = new List<String>();
            string newLine;
            if (Path.GetExtension(Pathname) != ".csv")
            {
                newLine = string.Format("文件编号,原始文件编号,文件格式,文件夹编号,相机编号,布设点位编号,拍摄日期,拍摄时间,工作天数,对象类别,物种名称,动物数量,性别,独立探测首张,备注,aitag,prediction,confirmed");
            }
            else
            {
                lines1[0] = lines1[0].Replace("\n", "");
                newLine = lines1[0].Replace("\r","")+",aitag,prediction,confirmed";
            }
            csv.AppendLine(newLine);

            string folderPath = Path.GetDirectoryName(ConfirmPredictions[0].FilePath);

            string speciesName = "";
            var lastPhotoSpecie = speciesName;
            string firstDetected = "";
            string speciesNamef = "";
            String imagePath = "";
            var newdir = processDirctory(ConfirmFolder, ConfirmPredictions[0].FilePath, RootProcessFolder);
            string pathString = System.IO.Path.Combine(newdir, positionNumber);
            string speciesConfig;
            System.IO.Directory.CreateDirectory(pathString);
            var OriginalTime = File.GetLastWriteTime(ConfirmPredictions[0].FilePath);
            foreach (var model in ConfirmPredictions)
            {

                imagePath = model.FilePath;
                if (!File.Exists(imagePath))
                {
                    continue;
                }
                var shootingDate = (cortime + (File.GetLastWriteTime(imagePath) - OriginalTime)).Date;
                var shootingTime = (cortime + (File.GetLastWriteTime(imagePath) - OriginalTime)).ToShortTimeString();
                var fileNameNoext = Path.GetFileNameWithoutExtension(imagePath);
                var fileExt = Path.GetExtension(imagePath);
                workDays = (shootingDate - lastWorkDay).Days;
                if (model.IsPhoto == true)
                {
                    Bitmap bmp = new Bitmap(imagePath);
                    var photoPath = (Path.GetExtension(Pathname) != ".csv")
                        ? $"{positionNumber}-{i.ToString("D4")}{fileExt}"
                        : Path.GetFileName(imagePath);
                    bmp.Save($"{Path.Combine(pathString, photoPath)}", System.Drawing.Imaging.ImageFormat.Jpeg);
                    var sorted = model.Predictions.OrderByDescending(f => f.Probability);
                    //this is the part for the threshhold
                    int CorrectedIndex = 0;
                    speciesName = sorted.ToList()[0].Tag;
                    speciesConfig = (new JavaScriptSerializer().Serialize(sorted.ToList())).Replace(",","|");
 
                    firstDetected = (speciesName == lastPhotoSpecie) ? "NO" : "YES";
                    lastPhotoSpecie = speciesName;
                    speciesNamef = speciesName;
                }
                else if (fileExt == ".AVI" || fileExt == ".avi")
                {
                    if (!File.Exists(Path.Combine(pathString, $"{positionNumber}-{i.ToString("D4")}{fileExt}")))
                    {
                        File.Copy(imagePath, Path.Combine(pathString, $"{positionNumber}-{i.ToString("D4")}{fileExt}"));
                    }

                    speciesConfig = "";
                    speciesNamef = "";
                    firstDetected = "";
                }
                else
                {
                    continue;
                }
                if(Path.GetExtension(Pathname) != ".csv")
                {
                    newLine = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17}",
                        $"{positionNumber}-{i.ToString("D4")}",
                        fileNameNoext,
                        fileExt.Replace(".", ""),
                        positionNumber,
                        cameraNumber,
                        positionNumber,
                        shootingDate,
                        shootingTime,
                        workDays,
                        "",
                        speciesNamef,
                        "",
                        "",
                        firstDetected,
                        "",
                        speciesNamef,
                        speciesConfig,
                        "no"
                    );
                }
                else
                {
                    lines1[indexs[i - 1] + 1]=lines1[indexs[i - 1] + 1].Replace("\r", "");
                    newLine = lines1[indexs[i-1]+1].Replace("\n","")+ $",{speciesNamef},{speciesConfig},no" ;
                }
                csv.AppendLine(newLine);
                this.Dispatcher.Invoke(() =>
                {
                    double a = i;
                    this.UploadingBar.Value = a / ConfirmPredictions.Count * 100;
                });
                i++;
            }


            //var datadir = processDirctory(DataVisFolder, ConfirmPredictions[0].FilePath, RootProcessFolder);
            try
            {
                File.WriteAllText(Path.Combine(folderPath, "done.txt"), "", Encoding.Default);
                File.WriteAllText(Path.Combine(pathString, $"{positionNumber}.csv"), csv.ToString(), Encoding.Default);
            }
            catch (Exception e)
            {
                richTextBox1.Text = "could not open the file, becasue it is opend else where";
                return;
            }

            this.Dispatcher.Invoke(() => {
                this.UploadingBar.Value = 100;
                this.richTextBox1.Text = ("Done");
                this.ConfirmButton.Visibility = Visibility.Hidden;
                this.ConfirmTextBox.Visibility = Visibility.Hidden;

            });
            return;

        }

        void processCSV(string pathname)
        {
            StreamReader reader;
            try
            {
                reader = new StreamReader(pathname, Encoding.Default);
            }
            catch (Exception e)
            {
                richTextBox1.Text = "could not onpen the csv,becuase it is already opened somewhere else";

                return;
            }

            var file = reader.ReadToEnd();
            reader.Close();
            var lines = file.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            var attributes = lines[0].Split(',');
            attributes[attributes.Length - 1] = attributes[attributes.Length - 1].Replace("\r", "");
            var csv = new StringBuilder();
            csv.AppendLine(lines[0]);
            int i = 0;
            foreach (var line in lines)
            {

                if (i == 0)
                {
                    i++;
                    continue;
                }

                var elements = line.Split(',');
                var elementsList = new List<string>(elements);
                while (elementsList.Count<attributes.Length)
                {
                    elementsList.Add("");
                }
                var new_line = String.Join(",", elementsList);
                csv.AppendLine(new_line);
            }
            File.WriteAllText(pathname, csv.ToString(), Encoding.Default);

        }
        string processDirctory(string targetfolder,string Pathname,string Rootdir)
        {
            List<string> directoryList = new List<string>();
            if (Rootdir == Path.GetFileName(Path.GetDirectoryName(Pathname)))
            {
                return targetfolder;
            }
            string pp = Path.GetDirectoryName(Path.GetDirectoryName(Pathname));
            string directoryName = Path.GetFileName(pp);
            while (Rootdir != directoryName)
            {
                pp = Path.GetDirectoryName(pp);
                directoryList.Add(directoryName);
                directoryName = Path.GetFileName(pp);
            }
            directoryList.Add(directoryName);
            directoryList.Reverse();
            var newdir = targetfolder;
            foreach (var name in directoryList)
            {
                newdir = Path.Combine(newdir, name);
            }
            Directory.CreateDirectory(newdir);
            return newdir;
        }
        async void StartPrediction1(Action< List<string>>iterator)
        {
            SinorMul = true;
            List<string> directorys = new List<string>();
            IndexCamera = 0;
            CameraLocations = new List<string>();
            CameraNumbers = new List<string>();

            this.ListV.ItemsSource = new List<String>();
            this.UploadingBar.Value = 0;
            this.richTextBox1.Text="";
            this.pictureBox1.Source = new BitmapImage();
            this.ConfirmButton.Visibility = Visibility.Hidden;
            this.ConfirmTextBox.Visibility = Visibility.Hidden;

            List<string> fileorflolder = new List<string>();
            fileorflolder.Add("process file");
            fileorflolder.Add("process all csv in folder or subfolder");
            fileorflolder.Add("process all photos in foler or subfoler");
            int index;
            if (PredictionOnly)
            {
                 index = 2;
            }
            else
            {
                ConfirmDIalog conDlg = new ConfirmDIalog(fileorflolder);

                // Configure the dialog box
                conDlg.Owner = this;
                // Open the dialog box modally 
                conDlg.ShowDialog();
                 index = conDlg.Path;
            }



            List<string> imagePaths = new List<string>();
            string Pathname;
            List<List<string>> imagePath_2 = new List<List<string>>();
            if (index == 0 )
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Multiselect = true;
                openFileDialog.ShowDialog();

                if (!File.Exists(openFileDialog.FileName))
                {
                    richTextBox1.Text = "invliad file chosse";
                    return;
                }
                imagePaths = new List<string>(openFileDialog.FileNames);
                RootProcessFolder = Path.GetFileName(Path.GetDirectoryName(openFileDialog.FileName));
                directorys.Add(Path.GetDirectoryName(openFileDialog.FileName));
                imagePath_2.Add(imagePaths);

                //singleProcess(imagePaths);

            }

            if (index == 1)
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    DialogResult result = fbd.ShowDialog();

                    if ( !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        RootProcessFolder = Path.GetFileName(fbd.SelectedPath);
                        DirectoryInfo d = new DirectoryInfo(fbd.SelectedPath);
                        
                        var files = d.GetFiles("*.csv", SearchOption.AllDirectories);

                        foreach (var file in files)
                        {
                            d=new DirectoryInfo(file.DirectoryName);
                            var xlsxs = d.GetFiles("*.xlsx", SearchOption.TopDirectoryOnly);
                            if (xlsxs.Length != 0)
                            {
                                XlstoCsv.convertExcelToCSV(xlsxs[0].FullName, file.FullName);
                            }
                            Pathname = file.FullName;
                            if (Directory.GetFiles(Path.GetDirectoryName(Pathname),"done.txt",SearchOption.TopDirectoryOnly).Length!=0)
                            {
                                if (IsContinue) continue;
                            }
                                imagePaths = new List<string>();
                                imagePaths.Add(Pathname);
                                imagePath_2.Add(imagePaths);
  
                            //singleProcess(imagePaths);
                        }
                    }
                }

            }
            else if (index == 2)
            {
                SinorMul = false;
                using (var fbd = new FolderBrowserDialog())
                {
                    DialogResult result = fbd.ShowDialog();

                    if (!string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        RootProcessFolder = Path.GetFileName(fbd.SelectedPath);
                        var files = Directory.GetFiles(fbd.SelectedPath,"*.JPG",SearchOption.AllDirectories);
                        List<string> imagepaths = new List<string>();
                        string dir = "testLOOLOL";

                        foreach (var file in files)
                        {
                            var dirpath = Path.GetDirectoryName(file);
                            if (dirpath != dir)
                            {
                                var a = Directory.GetFiles(dirpath, "done.txt", SearchOption.TopDirectoryOnly);
                                if (a.Length != 0)
                                {
                                    if (!IsContinue)
                                    {
                                       File.Delete(a[0]);
                                       directorys.Add(dirpath);
                                       dir = dirpath;
                                    }
                                }
                                else
                                {
                                    directorys.Add(dirpath);
                                    dir = dirpath;
                                }

                                
                            }

                        }
                    }
                    else
                    {
                        richTextBox1.Text = "not valid path";
                        return;
                    }
                }

                foreach (var dir in directorys)
                {
                    imagePath_2.Add(new List<string>(Directory.GetFiles(dir)));
                }
                if (!PredictionOnly)
                {
                    camerainformationwithdirectory cawindow = new camerainformationwithdirectory(directorys);
                    cawindow.Top = this.Top + 20;
                    cawindow.ShowDialog();
                    if (cawindow.CameraNumbers.Count != directorys.Count || cawindow.CameraLocations.Count != directorys.Count)
                    {
                        richTextBox1.Text = "invlid cameralocation typed";
                        return;
                    }
                    CameraNumbers = cawindow.CameraNumbers;
                    CameraLocations = cawindow.CameraLocations;
                    CorrectedTime = cawindow.DateTimes;
                }
            }

            int i = 1;
            
            foreach (var filepaths in imagePath_2)
            {
                this.FolderProgressBox.Text = $"Process {i} of  {imagePath_2.Count} folders";
                AllowUIToUpdate();
                iterator(filepaths);
                i++;
            }

            return;
        }
        private void GenerateCSV2(List<string> originalLocation1)
        {
            this.Dispatcher.Invoke(() =>
            {
                this.UploadingBar.Value = 0;
                AllowUIToUpdate();
            });
            int workDays = 1;
            int i = 0;
            //IDictionary<string, int> dict = new Dictionary<string, int>();
            IDictionary<string, int> dictAttribute = new Dictionary<string, int>();
            StreamReader reader;
            var originalLocation = originalLocation1[0];
            if (Path.GetExtension(originalLocation) != ".csv")
            {
                this.Dispatcher.Invoke(() =>
                {
                    this.richTextBox1.Text = "only csv can be confirmed!";
                });
                return;
            }
            try
            {
                reader = new StreamReader(originalLocation, Encoding.Default);
            }
            catch (Exception e)
            {
                richTextBox1.Text = "could not onpen the csv,becuase it is already opened somewhere else";

                return;
            }

            var file = reader.ReadToEnd();
            reader.Close();
            var lines = file.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            var attributes = lines[0].Split(',');
            attributes[attributes.Length - 1] = attributes[attributes.Length - 1].Replace("\r", "");
            foreach (var attribute in attributes)
            {
                dictAttribute.Add(attribute, i);
                i++;
            }

            if (!dictAttribute.ContainsKey("prediction"))
            {
                this.Dispatcher.Invoke(() => {
                    this.richTextBox1.Text = "not valid csv chosen!";
                });
                return;
            }


            var csv = new StringBuilder();


            string speciesName = "";
            var lastPhotoSpecie = speciesName;
            string firstDetected = "";
            string speciesNamef = "";
            string cameraLocation = "";

            csv.AppendLine(lines[0].Replace("\r", "").Replace("\n", ""));



            i = 0;
            foreach (var line in lines)
            {

                if (i == 0)
                {
                    i++;
                    continue;
                }
                var elements = lines[i].Split(',');
                if (elements.Length < 3)
                {
                    continue;
                }
                //var predictionIndex = elements[dictAttribute["prediction"]];
                //imagePath = model.FilePath;
                //if (!File.Exists(imagePath))
                //{
                //    continue;
                //}

                var fileNameNoext = elements[dictAttribute["文件编号"]];
                var fileExt = elements[dictAttribute["文件格式"]];
                var prediction = elements[dictAttribute["prediction"]];
                var imagePath = Path.Combine(Path.GetDirectoryName(originalLocation), $"{fileNameNoext}.{fileExt}");

                //workDays = (shootingDate - lastWorkDay).Days;
                List<Prediction> sorted = JsonConvert.DeserializeObject<List<Prediction>>(prediction.Replace("|", ","));
                LocalSorted = sorted;
                if (fileExt == "JPG" || fileExt == "jpg")
                {
                    if (IsContinue && elements[dictAttribute["confirmed"]] == "yes")
                    {
                        csv.AppendLine(line);
                        this.Dispatcher.Invoke(() =>
                        {
                            double a = i;
                            this.UploadingBar.Value = a / (lines.Length - 1) * 100;
                        });
                        AllowUIToUpdate();
                        i++;
                        continue;
                    }
                    //this is the part for the threshhold
                    int CorrectedIndex = 0;
                    speciesName = sorted.ToList()[0].Tag;
                    if (UploadOnly)
                    {

                        var animalImage = new AnimalImage();
                        animalImage.ImageName = elements[dictAttribute["文件编号"]] + '.' + elements[dictAttribute["文件格式"]];
                        animalImage.Tag = (elements[dictAttribute["物种名称"]] == "")
                            ? elements[dictAttribute["aitag"]]
                            : elements[dictAttribute["物种名称"]];
                        animalImage.LocationName = elements[dictAttribute["布设点位编号"]];
                        UploadImage(imagePath, animalImage);
                        this.Dispatcher.Invoke(() =>
                        {
                            double a = i;
                            this.UploadingBar.Value = a / (lines.Length - 1) * 100;
                        });
                        AllowUIToUpdate();
                        i++;
                        continue;
                    }

                    bool is_threshhold = true;
                    this.Dispatcher.Invoke(() =>
                    {
                        is_threshhold = this.isThreshhold;
                    });
                    if (Math.Round(sorted.ToList()[0].Probability * 100, 6) <= this.ConfidenceRate || is_threshhold == false) //should be 
                    {

                        var items = new List<string>();
                        foreach (var pre in sorted)
                        {
                            // this.richTextBox1.Text += pre.Tag + ":  " + pre.Probability.ToString("P", CultureInfo.InvariantCulture) + "\n";
                            items.Add(pre.Tag + ":  " + Math.Round(pre.Probability * 100, 6) + "%\n");

                        }
                        this.Dispatcher.Invoke(() =>
                        {
                            //this.pictureBox1.Source = new BitmapImage(new Uri(imagePath));
                            var theGrid = new System.Windows.Controls.Grid();
                            theGrid.Width = 220;
                            theGrid.Height = 200;
                            theGrid.VerticalAlignment = VerticalAlignment.Top;
                            var cm = new System.Windows.Controls.ContextMenu() {
                                StaysOpen=true
                            };
                        for (var index = 1; index < items.Count()-1; index++) {
                                var mi = new System.Windows.Controls.MenuItem()
                                {
                                    FontSize = 30,
                                    FontFamily = new System.Windows.Media.FontFamily("Century Gothic"),
                                    Header=items[i]
                                 
                                };
                                cm.Items.Add(mi);
                            }
                            var textbox = new System.Windows.Controls.Button() {
                                VerticalAlignment = VerticalAlignment.Bottom,
                                Width = 220,
                                Height = 40,
                                FontSize = 30,
                                FontFamily = new System.Windows.Media.FontFamily("Century Gothic"),
                                Content = items[0],
                                ContextMenu=cm,
                                Foreground = System.Windows.Media.Brushes.White,
                                Background= System.Windows.Media.Brushes.Transparent,
                                BorderBrush= System.Windows.Media.Brushes.Transparent
                            };
                            var theimage = new System.Windows.Controls.Image() {
                                VerticalAlignment = VerticalAlignment.Top,
                                Width=200,
                                Height = 180,
                                Source = new BitmapImage(new Uri(imagePath))
                            };
                            theGrid.Children.Add(theimage);
                            theGrid.Children.Add(textbox);
                            this.ViewStackPanel.Children.Add(theGrid);
                            //this.richTextBox1.Text =
                            //    "please reconfirm the Animal that below confidence rate " + $"{ConfidenceRate}%";
                            ////do something to the window
                            //this.ListV.ItemsSource = new List<String>();
                            //this.ListV.ItemsSource = items;
                            //this.ConfirmButton.Visibility = Visibility.Visible;
                            //this.ConfirmTextBox.Visibility = Visibility.Visible;
                            //Canvas.SetTop(BoundingBox, LocalSorted[0].Region.Top * 368);
                            //Canvas.SetLeft(BoundingBox, LocalSorted[0].Region.Left * 560);
                            //this.BoundingBox.Width = LocalSorted[0].Region.Width * 560;
                            //this.BoundingBox.Height = LocalSorted[0].Region.Height * 368;
                            //BoundingBox.Visibility = Visibility.Visible;
                        });
                        speciesName = sorted.ToList<Prediction>()[0].Tag;

                        //AllowUIToUpdate();
                        //mre.WaitOne();
                        //mre.Reset();

                        this.Dispatcher.Invoke(() =>
                        {
                            if (this.ListV.SelectedIndex != -1)
                            {
                                CorrectedIndex = this.ListV.SelectedIndex;
                                speciesName = sorted.ToList<Prediction>()[CorrectedIndex].Tag;
                            }
                            else
                            {
                                speciesName = ConfirmTextBox.Text;
                            }
                            elements[dictAttribute["物种名称"]] = speciesName;

                        });

                    }

                    elements[dictAttribute["confirmed"]] = "yes";
                    firstDetected = (speciesName == lastPhotoSpecie) ? "否" : "是";
                    lastPhotoSpecie = speciesName;
                    speciesNamef = speciesName;
                    //elements[dictAttribute["aitag"]] = speciesNamef;
                    if (elements[dictAttribute["物种名称"]] == "")
                    {
                        elements[dictAttribute["物种名称"]] = speciesNamef;
                    }
                    elements[dictAttribute["独立探测首张"]] = firstDetected;
                    var new_line = String.Join(",", elements);
                    csv.AppendLine(new_line.Replace("\r", "").Replace("\n", ""));
                }
                else
                {
                    csv.AppendLine(line.Replace("\r", "").Replace("\n", ""));
                    i++;
                    continue;
                }


                this.Dispatcher.Invoke(() =>
                {
                    double a = i;
                    this.UploadingBar.Value = a / (lines.Length - 1) * 100;
                });
                i++;
                AllowUIToUpdate();
            }

            if (UploadOnly)
            {
                return;
            }
            cameraLocation = (lines[1].Split(',')[dictAttribute["布设点位编号"]]);

            //File.WriteAllText(Path.Combine(pathString, "done.txt"), "", Encoding.Default);

            File.WriteAllText(originalLocation, csv.ToString(), Encoding.Default);
            this.Dispatcher.Invoke(() => {
                this.UploadingBar.Value = 100;
                this.richTextBox1.Text = ("Done");
                this.ConfirmButton.Visibility = Visibility.Hidden;
                this.ConfirmTextBox.Visibility = Visibility.Hidden;

            });





        }
            private void GenerateCSV1(List<string> originalLocation1)
        {
            this.Dispatcher.Invoke(() =>
            {
                this.UploadingBar.Value = 0;
                AllowUIToUpdate();
            });
            int workDays = 1;
            int i = 0;
            //IDictionary<string, int> dict = new Dictionary<string, int>();
            IDictionary<string, int> dictAttribute = new Dictionary<string, int>();
            StreamReader reader;
            var originalLocation = originalLocation1[0];
            if (Path.GetExtension(originalLocation) != ".csv")
            {
                this.Dispatcher.Invoke(() =>
                {
                    this.richTextBox1.Text="only csv can be confirmed!";
                });
                return;
            }
            try
            {
                 reader = new StreamReader(originalLocation, Encoding.Default);
            }
            catch (Exception e)
            {
                richTextBox1.Text = "could not onpen the csv,becuase it is already opened somewhere else";

                return;
            }
           
            var file = reader.ReadToEnd();
            reader.Close();
            var lines = file.Split("\r\n".ToCharArray(),StringSplitOptions.RemoveEmptyEntries);
            var attributes = lines[0].Split(',');
            attributes[attributes.Length - 1] = attributes[attributes.Length - 1].Replace("\r", "");
            foreach (var attribute in attributes)
            {
                dictAttribute.Add(attribute,i);
                i++;
            }

            if (!dictAttribute.ContainsKey("prediction"))
            {
                this.Dispatcher.Invoke(()=>{
                    this.richTextBox1.Text = "not valid csv chosen!";
                });
                return;
            }

           
            var csv = new StringBuilder();
            
            
            string speciesName = "";
            var lastPhotoSpecie = speciesName;
            string firstDetected = "";
            string speciesNamef = "";
            string cameraLocation = "";

            csv.AppendLine(lines[0].Replace("\r", "").Replace("\n", ""));


            
            i = 0;
            foreach (var line in lines)
            {

                if (i == 0)
                {
                    i++;
                    continue;
                }
                var elements = lines[i].Split(',');
                if (elements.Length < 3)
                {
                    continue;
                }
                //var predictionIndex = elements[dictAttribute["prediction"]];
                //imagePath = model.FilePath;
                //if (!File.Exists(imagePath))
                //{
                //    continue;
                //}
                
                var fileNameNoext = elements[dictAttribute["文件编号"]];
                var fileExt = elements[dictAttribute["文件格式"]];
                var prediction = elements[dictAttribute["prediction"]];
                var imagePath = Path.Combine(Path.GetDirectoryName(originalLocation) , $"{fileNameNoext}.{fileExt}");

                //workDays = (shootingDate - lastWorkDay).Days;
                List<Prediction> sorted = JsonConvert.DeserializeObject<List<Prediction>>(prediction.Replace("|",","));
                LocalSorted = sorted;
                if (fileExt == "JPG"|| fileExt=="jpg")
                {
                    if (IsContinue&& elements[dictAttribute["confirmed"]] == "yes")
                    {
                        csv.AppendLine(line);
                        this.Dispatcher.Invoke(() =>
                        {
                            double a = i;
                            this.UploadingBar.Value = a / (lines.Length - 1) * 100;
                        });
                        AllowUIToUpdate();
                        i++;
                        continue;
                    }
                    //this is the part for the threshhold
                    int CorrectedIndex = 0;
                    speciesName = sorted.ToList()[0].Tag;
                    if (UploadOnly)
                    {
                        
                        var animalImage = new AnimalImage();
                        animalImage.ImageName = elements[dictAttribute["文件编号"]]+'.'+elements[dictAttribute["文件格式"]];
                        animalImage.Tag = (elements[dictAttribute["物种名称"]] == "")
                            ? elements[dictAttribute["aitag"]]
                            : elements[dictAttribute["物种名称"]];
                        animalImage.LocationName= elements[dictAttribute["布设点位编号"]];
                        UploadImage(imagePath, animalImage);
                        this.Dispatcher.Invoke(() =>
                        {
                            double a = i;
                            this.UploadingBar.Value = a / (lines.Length - 1) * 100;
                        });
                        AllowUIToUpdate();
                        i++;
                        continue;
                    }

                    bool is_threshhold = true;
                    this.Dispatcher.Invoke(() =>
                    {
                         is_threshhold = this.isThreshhold;
                    });
                    if (Math.Round(sorted.ToList()[0].Probability * 100, 6) <= this.ConfidenceRate || is_threshhold==false) //should be 
                    {

                        var items = new List<string>();
                        foreach (var pre in sorted)
                        {
                            // this.richTextBox1.Text += pre.Tag + ":  " + pre.Probability.ToString("P", CultureInfo.InvariantCulture) + "\n";
                            items.Add(pre.Tag + ":  " + Math.Round(pre.Probability * 100, 6) + "%\n");

                        }
                        this.Dispatcher.Invoke(() =>
                        {
                            this.pictureBox1.Source = new BitmapImage(new Uri(imagePath));
                            this.richTextBox1.Text =
                                "please reconfirm the Animal that below confidence rate " + $"{ConfidenceRate}%";
                            //do something to the window
                            this.ListV.ItemsSource = new List<String>();
                            this.ListV.ItemsSource = items;
                            this.ConfirmButton.Visibility = Visibility.Visible;
                            this.ConfirmTextBox.Visibility = Visibility.Visible;
                                Canvas.SetTop(BoundingBox, LocalSorted[0].Region.Top * 368);
                                Canvas.SetLeft(BoundingBox, LocalSorted[0].Region.Left * 560);
                                this.BoundingBox.Width = LocalSorted[0].Region.Width * 560;
                                this.BoundingBox.Height = LocalSorted[0].Region.Height * 368;
                            BoundingBox.Visibility = Visibility.Visible;
                        });

                        //AllowUIToUpdate();
                        mre.WaitOne();
                        mre.Reset();

                        this.Dispatcher.Invoke(() =>
                        {
                            if (this.ListV.SelectedIndex != -1)
                            {
                                CorrectedIndex = this.ListV.SelectedIndex;
                                speciesName = sorted.ToList<Prediction>()[CorrectedIndex].Tag;
                            }
                            else
                            {
                                speciesName = ConfirmTextBox.Text;
                            }
                            elements[dictAttribute["物种名称"]] = speciesName;

                        });

                    }

                    elements[dictAttribute["confirmed"]] = "yes";
                    firstDetected = (speciesName == lastPhotoSpecie) ? "否" : "是";
                    lastPhotoSpecie = speciesName;
                    speciesNamef = speciesName;
                    //elements[dictAttribute["aitag"]] = speciesNamef;
                    if (elements[dictAttribute["物种名称"]] == "")
                    {
                        elements[dictAttribute["物种名称"]] = speciesNamef;
                    }
                    elements[dictAttribute["独立探测首张"]] = firstDetected;
                    var new_line = String.Join(",",elements);
                    csv.AppendLine(new_line.Replace("\r","").Replace("\n",""));
                }
                else
                {
                    csv.AppendLine(line.Replace("\r", "").Replace("\n", ""));
                    i++;
                    continue;
                }                


                this.Dispatcher.Invoke(() =>
                {
                    double a = i;
                    this.UploadingBar.Value = a / (lines.Length-1) *100;
                });
                    i++;
                AllowUIToUpdate();
            }

            if (UploadOnly)
            {
                return;
            }
            cameraLocation = (lines[1].Split(',')[dictAttribute["布设点位编号"]]);

            //File.WriteAllText(Path.Combine(pathString, "done.txt"), "", Encoding.Default);
            
            File.WriteAllText(originalLocation, csv.ToString(), Encoding.Default);
            this.Dispatcher.Invoke(() => {
                this.UploadingBar.Value = 100;
                this.richTextBox1.Text = ("Done");
                this.ConfirmButton.Visibility = Visibility.Hidden;
                this.ConfirmTextBox.Visibility = Visibility.Hidden;

            });
            //if (is_threshhold)
            //{
            //    var browse = processDirctory(BrowseFolder, models[0].FilePath, Rootdir);
            //    File.Move(originalLocation, Path.Combine(browse, $"{cameraLocation}.tnc"));
            //}
            return;
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            isThreshhold = false;
            confrim_photos();
            //isThreshhold = true;
        }

        private void Continuation_Checked(object sender, RoutedEventArgs e)
        {
            IsContinue = true;
            ContinuationBox.Text = "Continue On";
        }

        private void Continuation_Unchecked(object sender, RoutedEventArgs e)
        {
            IsContinue = false;
            ContinuationBox.Text = "Continue Off";
        }

        private void Upload_Click(object sender, RoutedEventArgs e)
        {
            //if(StateLocal) return;
            UploadOnly = true;
            UploadLocation();
            StartPrediction1(GenerateCSV1);
            UploadOnly = false;
        }

        public void UploadLocation()
        {
            StreamReader locationReader = new StreamReader(AnimalLocationTextFile);
            var locations = locationReader.ReadToEnd();
            locationReader.Close();
            UploadLocation(locations);
        }

        public void ManageLocation()
        {

            StreamReader locationReader = new StreamReader(AnimalLocationTextFile);
            List<int> indexes = new List<int>();
            if (locationReader.Peek()==-1)
            {
                locationReader.Close();
                return;
            }
            else
            {
                List<string> stringlocationList = new List<string>();
                var locatonjson = locationReader.ReadToEnd();
                List<AnimalLocation> AnimalLocations = JsonConvert.DeserializeObject<List<AnimalLocation>>(locatonjson);
                
                int i = 0;
                foreach (var animallocation in AnimalLocations)
                {
                    if (animallocation.Latitude == 0)
                    {
                        stringlocationList.Add(animallocation.Name);
                        indexes.Add(i);
                    }

                    i++;
                }

                
               var a = Microsoft.VisualBasic.Interaction.InputBox("please type in the location information below, the format is (longtitude,latitude)|(longtitude,latitude)\n" + String.Join("\n", stringlocationList.ToArray() ),
                    "location addtion addition",
                    "Default data",
                    -1, -1);
                var resultstrings = a.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                i = 0;
                foreach (var resultstring in resultstrings)
                {
                    var removeParaphesestring =resultstring.Trim(new Char[] {'(', ')'});
                    var locationinformation = removeParaphesestring.Split(',');
                    AnimalLocations[indexes[i]].Latitude = Convert.ToDouble(locationinformation[0]);
                    AnimalLocations[indexes[i]].Longtitude = Convert.ToDouble(locationinformation[1]);
                    i++;
                }
                locationReader.Close();
                var locationjson = JsonConvert.SerializeObject(AnimalLocations);
                File.WriteAllText(AnimalLocationTextFile,locationjson);
            }

            

        }
        //private void Upload()
        //{
        //    SinorMul = true;
        //    List<string> directorys = new List<string>();
        //    IndexCamera = 0;
        //    CameraLocations = new List<string>();
        //    CameraNumbers = new List<string>();

        //    this.ListV.ItemsSource = new List<String>();
        //    this.UploadingBar.Value = 0;
        //    this.richTextBox1.Text = "";
        //    this.pictureBox1.Source = new BitmapImage();
        //    this.ConfirmButton.Visibility = Visibility.Hidden;
        //    this.ConfirmTextBox.Visibility = Visibility.Hidden;

        //    List<string> fileorflolder = new List<string>();
        //    fileorflolder.Add("process csv");
        //    fileorflolder.Add("process all csv in folder or subfolder");
        //    ConfirmDIalog conDlg = new ConfirmDIalog(fileorflolder);

        //    // Configure the dialog box
        //    conDlg.Owner = this;
        //    // Open the dialog box modally 
        //    conDlg.ShowDialog();
        //    int index = conDlg.Path;
        //    List<string> imagePaths = new List<string>();
        //    string Pathname;
        //    List<List<string>> imagePath_2 = new List<List<string>>();
        //    if (index == 0)
        //    {
        //        OpenFileDialog openFileDialog = new OpenFileDialog();
        //        openFileDialog.Multiselect = true;
        //        openFileDialog.ShowDialog();

        //        if (!File.Exists(openFileDialog.FileName))
        //        {
        //            richTextBox1.Text = "invliad file chosse";
        //            return;
        //        }
        //        imagePaths = new List<string>(openFileDialog.FileNames);
        //        RootProcessFolder = Path.GetFileName(Path.GetDirectoryName(openFileDialog.FileName));
        //        directorys.Add(Path.GetDirectoryName(openFileDialog.FileName));
        //        imagePath_2.Add(imagePaths);

        //        //singleProcess(imagePaths);

        //    }

        //    if (index == 1)
        //    {
        //        using (var fbd = new FolderBrowserDialog())
        //        {
        //            DialogResult result = fbd.ShowDialog();

        //            if (!string.IsNullOrWhiteSpace(fbd.SelectedPath))
        //            {
        //                RootProcessFolder = Path.GetFileName(fbd.SelectedPath);
        //                DirectoryInfo d = new DirectoryInfo(fbd.SelectedPath);

        //                var files = d.GetFiles("*.csv", SearchOption.AllDirectories);

        //                foreach (var file in files)
        //                {
        //                    Pathname = file.FullName;
        //                    if (Directory.GetFiles(Path.GetDirectoryName(Pathname), "done.txt", SearchOption.TopDirectoryOnly).Length != 0)
        //                    {
        //                        if (IsContinue) continue;
        //                    }
        //                    imagePaths = new List<string>();
        //                    imagePaths.Add(Pathname);
        //                    imagePath_2.Add(imagePaths);

        //                    //singleProcess(imagePaths);
        //                }
        //            }
        //        }

        //    }

        //    int i = 1;

        //    foreach (var filepaths in imagePath_2)
        //    {
        //        this.FolderProgressBox.Text = $"Process {i} of  {imagePath_2.Count} folders";
        //        AllowUIToUpdate();
        //        singleProcess(filepaths);
        //        i++;
        //    }
        //    this.FolderProgressBox.Text = "Done";
        //    return;
        //}

        private void ConfidenceBar_ValueChanged_1(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.ConfidenceRate = (int)ConfidenceBar.Value;
        }

        public void UpdateAnimalLocation(string newLocation)
        {
            StreamReader sr = new StreamReader(AnimalLocationTextFile);
            List<AnimalLocation> animallocations;
            string jsonstring;
            if (sr.Peek() == -1)
            {
                animallocations = new List<AnimalLocation>()
                {
                    new AnimalLocation()
                    {
                        Name = newLocation
                    }
                };
                
            }
            else
            {
                jsonstring = sr.ReadToEnd();
                animallocations = JsonConvert.DeserializeObject<List<AnimalLocation>>(jsonstring);
                if (animallocations.Where(p=>p.Name==newLocation).Count()==0)
                {
                    animallocations.Add(new AnimalLocation()
                    {
                        Name = newLocation
                    });
                }

            }
            sr.Close();
            jsonstring = JsonConvert.SerializeObject(animallocations);
            File.WriteAllText(AnimalLocationTextFile,jsonstring);
        }
        private void ManageLocation(object sender, RoutedEventArgs e)
        {
            ManageLocation();
        }

        private void ListV_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (this.BoundingBox.Visibility == Visibility.Hidden) return;
            if (ListV.SelectedIndex < LocalSorted.Count+1 && ListV.SelectedIndex>=0)
            {
                Canvas.SetTop(BoundingBox, LocalSorted[ListV.SelectedIndex].Region.Top * 368);
                Canvas.SetLeft(BoundingBox, LocalSorted[ListV.SelectedIndex].Region.Left * 560);
                this.BoundingBox.Width = LocalSorted[ListV.SelectedIndex].Region.Width * 560;
                this.BoundingBox.Height = LocalSorted[ListV.SelectedIndex].Region.Height * 368;
            }

        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {


        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigGrid.Visibility == Visibility.Collapsed)
                ConfigGrid.Visibility = Visibility.Visible;
            else ConfigGrid.Visibility = Visibility.Collapsed;
            //Window2 dlg = new Window2();
            //Ask for camera information
            //// Configure the dialog box
            //dlg.Owner = this;
            //// Open the dialog box modally 
            //dlg.ShowDialog();
            //dlg.Top = this.Top + 20;
            //this.ConfidenceRate = (int)dlg.ConfidenceBar.Value;
            //this.isThreshhold = dlg.Continuation.IsChecked ??true;
            //this.StateLocal = dlg.Online_status_checkbox.IsChecked ?? true;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ConfigGrid.Visibility = Visibility.Collapsed;
        }

        private void PredictionOnlyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            PredictionOnly = true;
        }


        private void PredictionOnlyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            PredictionOnly = false;
            ConfirmFolder = RootFolder;
            System.IO.Directory.CreateDirectory(ConfirmFolder);
        }
    }
}
