using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;

namespace ATMML
{
    class AutoML
    {
        static bool _appStore = false;
        static string _baseDir = "";

        public static void Calculate(string[] args)
        {
            //Console.WriteLine("Start");
            //for (var i = 0; i < args.Length; i++)
            //{
            //    Console.WriteLine("  " + i + "  " + args[i]);
            //}

            try
            {
                if (args.Length >= 3)
                {
                    _baseDir = args[0];
                    var path = args[1];
                    var mode = args[2]; // train or test
                    var maxTime = (args.Length >= 4) ? args[3] : "3";
                    var metric = (args.Length >= 5) ? args[4] : "ACCURACY";
                    var split = (args.Length >= 6) ? args[5] : "80";
                    var trained = (args.Length >= 7) ? bool.Parse(args[6]) : false;

                    if (mode == "train_binary")
                    {
                        runBinaryExperiment(path, maxTime, metric, split, trained);
                    }
                    else if (mode == "train_regression")
                    {
                        runRegressionExperiment(path, maxTime, metric, split, trained);
                    }
                    else if (mode == "predict_binary")
                    {
                        predictBinary(path);
                    }
                    else if (mode == "predict_regression")
                    {
                        predictRegression(path);
                    }
                }
            }
            catch (Exception x)
            {
                Console.WriteLine("Exception: " + x);
            }
           // Console.WriteLine("End");
        }

        public static void SaveUserData(string path, string data, bool useLocalDatabase = false)
        {
            if (_appStore)
            {
                //CachedData.SaveData<string>(path.Replace('\', '-'), data);
            }
            else
            {
                var folders = path.Split('\\');
                var folderPath = _baseDir;
                for (var ii = 0; ii < folders.Length - 1; ii++)
                {
                    folderPath += @"\" + folders[ii];
                    Directory.CreateDirectory(folderPath);
                }

                try
                {
                    // uses MainView.GetDataFolder() as base
                    var sw = new StreamWriter(_baseDir + @"\" + path); // Save user settings
                    using (sw)
                    {
                        sw.Write(data);
                    }
                }
                catch (Exception x)
                {
                    Console.WriteLine("SaveSettings exception: " + x.Message);
                }
            }
        }

        private static string LoadUserData(string path, bool useLocalDatabase = false)
        {
            string output = "";
            if (_appStore)
            {
                //output = CachedData.LoadData<string>(path.Replace('\', '-'));
            }
            else
            {
                try
                {
                    var sr = new StreamReader(_baseDir + @"\" + path);
                    using (sr)
                    {
                        output = sr.ReadToEnd();
                    }
                }
                catch (Exception x)
                {
                    Console.WriteLine("LoadSettings exception: " + x.Message);
                }
            }
            return output;
        }

        private class BinaryExperimentProgressHandler : IProgress<RunDetail<BinaryClassificationMetrics>>
        {
            public void Report(RunDetail<BinaryClassificationMetrics> value)
            {
                //Console.WriteLine(value.RuntimeInSeconds);
            }
        }

        private class RegressionExperimentProgressHandler : IProgress<RunDetail<RegressionMetrics>>
        {
            public void Report(RunDetail<RegressionMetrics> value)
            {
                //Console.WriteLine(value.RuntimeInSeconds);
            }
        }

        private static void predictBinary(string pathName)
        {
            MLContext mlContext = new MLContext();

            var filePath1 = _baseDir + "\\" + pathName + @"\model.zip";
            ITransformer mlModel = mlContext.Model.Load(filePath1, out DataViewSchema inputSchema);
            var predEngine = mlContext.Model.CreatePredictionEngine<BinaryModelInput, ModelOutput>(mlModel);

            // 1) read test.csv into List<ModelInput>
            var modelInputs = new List<BinaryModelInput>();
            var filePath2 = pathName + @"\test.csv";
            var lines = LoadUserData(filePath2).Split('\n');
            if (lines.Length > 0)
            {
                var header = lines[0];
                var columnNames = header.Split(',');
                for (var ii = 1; ii < lines.Length; ii++)
                {
                    var line = lines[ii];
                    if (line.Length > 0)
                    {
                        var fields = line.Split(',');
                        var modelInput = new BinaryModelInput();
                        modelInput.Output = (fields[0] == "1");
                        modelInput.Input1 = fields[1]; // ticker
                        modelInput.Input2 = fields[2]; // interval;date

                        modelInput.Input3 = (fields.Length > 3) ? float.Parse(fields[3]) : 0.0f;
                        modelInput.Input4 = (fields.Length > 4) ? float.Parse(fields[4]) : 0.0f;
                        modelInput.Input5 = (fields.Length > 5) ? float.Parse(fields[5]) : 0.0f;
                        modelInput.Input6 = (fields.Length > 6) ? float.Parse(fields[6]) : 0.0f;
                        modelInput.Input7 = (fields.Length > 7) ? float.Parse(fields[7]) : 0.0f;
                        modelInput.Input8 = (fields.Length > 8) ? float.Parse(fields[8]) : 0.0f;
                        modelInput.Input9 = (fields.Length > 9) ? float.Parse(fields[9]) : 0.0f;
                        modelInput.Input10 = (fields.Length > 10) ? float.Parse(fields[10]) : 0.0f;
                        modelInput.Input11 = (fields.Length > 11) ? float.Parse(fields[11]) : 0.0f;
                        modelInput.Input12 = (fields.Length > 12) ? float.Parse(fields[12]) : 0.0f;
                        modelInput.Input13 = (fields.Length > 13) ? float.Parse(fields[13]) : 0.0f;
                        modelInput.Input14 = (fields.Length > 14) ? float.Parse(fields[14]) : 0.0f;
                        modelInput.Input15 = (fields.Length > 15) ? float.Parse(fields[15]) : 0.0f;
                        modelInput.Input16 = (fields.Length > 16) ? float.Parse(fields[16]) : 0.0f;
                        modelInput.Input17 = (fields.Length > 17) ? float.Parse(fields[17]) : 0.0f;
                        modelInput.Input18 = (fields.Length > 18) ? float.Parse(fields[18]) : 0.0f;
                        modelInput.Input19 = (fields.Length > 19) ? float.Parse(fields[19]) : 0.0f;
                        modelInput.Input20 = (fields.Length > 20) ? float.Parse(fields[20]) : 0.0f;
                        modelInput.Input21 = (fields.Length > 21) ? float.Parse(fields[21]) : 0.0f;
                        modelInput.Input22 = (fields.Length > 22) ? float.Parse(fields[22]) : 0.0f;
                        modelInput.Input23 = (fields.Length > 23) ? float.Parse(fields[23]) : 0.0f;
                        modelInput.Input24 = (fields.Length > 24) ? float.Parse(fields[24]) : 0.0f;
                        modelInput.Input25 = (fields.Length > 25) ? float.Parse(fields[25]) : 0.0f;
                        modelInput.Input26 = (fields.Length > 26) ? float.Parse(fields[26]) : 0.0f;
                        modelInput.Input27 = (fields.Length > 27) ? float.Parse(fields[27]) : 0.0f;
                        modelInput.Input28 = (fields.Length > 28) ? float.Parse(fields[28]) : 0.0f;
                        modelInput.Input29 = (fields.Length > 29) ? float.Parse(fields[29]) : 0.0f;
                        modelInput.Input30 = (fields.Length > 30) ? float.Parse(fields[30]) : 0.0f;
                        modelInput.Input31 = (fields.Length > 31) ? float.Parse(fields[31]) : 0.0f;
                        modelInput.Input32 = (fields.Length > 32) ? float.Parse(fields[32]) : 0.0f;
                        modelInput.Input33 = (fields.Length > 33) ? float.Parse(fields[33]) : 0.0f;
                        modelInput.Input34 = (fields.Length > 34) ? float.Parse(fields[34]) : 0.0f;
                        modelInput.Input35 = (fields.Length > 35) ? float.Parse(fields[35]) : 0.0f;
                        modelInput.Input36 = (fields.Length > 36) ? float.Parse(fields[36]) : 0.0f;
                        modelInput.Input37 = (fields.Length > 37) ? float.Parse(fields[37]) : 0.0f;
                        modelInput.Input38 = (fields.Length > 38) ? float.Parse(fields[38]) : 0.0f;
                        modelInput.Input39 = (fields.Length > 39) ? float.Parse(fields[39]) : 0.0f;
                        modelInput.Input40 = (fields.Length > 40) ? float.Parse(fields[40]) : 0.0f;
                        modelInput.Input41 = (fields.Length > 41) ? float.Parse(fields[41]) : 0.0f;
                        modelInput.Input42 = (fields.Length > 42) ? float.Parse(fields[42]) : 0.0f;
                        modelInput.Input43 = (fields.Length > 43) ? float.Parse(fields[43]) : 0.0f;
                        modelInput.Input44 = (fields.Length > 44) ? float.Parse(fields[44]) : 0.0f;
                        modelInput.Input45 = (fields.Length > 45) ? float.Parse(fields[45]) : 0.0f;
                        modelInput.Input46 = (fields.Length > 46) ? float.Parse(fields[46]) : 0.0f;
                        modelInput.Input47 = (fields.Length > 47) ? float.Parse(fields[47]) : 0.0f;
                        modelInput.Input48 = (fields.Length > 48) ? float.Parse(fields[48]) : 0.0f;
                        modelInput.Input49 = (fields.Length > 49) ? float.Parse(fields[49]) : 0.0f;
                        modelInput.Input50 = (fields.Length > 50) ? float.Parse(fields[50]) : 0.0f;
                        modelInput.Input51 = (fields.Length > 51) ? float.Parse(fields[51]) : 0.0f;
                        modelInput.Input52 = (fields.Length > 52) ? float.Parse(fields[52]) : 0.0f;
                        modelInput.Input53 = (fields.Length > 53) ? float.Parse(fields[53]) : 0.0f;
                        modelInput.Input54 = (fields.Length > 54) ? float.Parse(fields[54]) : 0.0f;
                        modelInput.Input55 = (fields.Length > 55) ? float.Parse(fields[55]) : 0.0f;
                        modelInput.Input56 = (fields.Length > 56) ? float.Parse(fields[56]) : 0.0f;
                        modelInput.Input57 = (fields.Length > 57) ? float.Parse(fields[57]) : 0.0f;
                        modelInput.Input58 = (fields.Length > 58) ? float.Parse(fields[58]) : 0.0f;
                        modelInput.Input59 = (fields.Length > 59) ? float.Parse(fields[59]) : 0.0f;
                        modelInput.Input60 = (fields.Length > 60) ? float.Parse(fields[60]) : 0.0f;
                        modelInput.Input61 = (fields.Length > 61) ? float.Parse(fields[61]) : 0.0f;
                        modelInput.Input62 = (fields.Length > 62) ? float.Parse(fields[62]) : 0.0f;
                        modelInput.Input63 = (fields.Length > 63) ? float.Parse(fields[63]) : 0.0f;
                        modelInput.Input64 = (fields.Length > 64) ? float.Parse(fields[64]) : 0.0f;
                        modelInput.Input65 = (fields.Length > 65) ? float.Parse(fields[65]) : 0.0f;
                        modelInput.Input66 = (fields.Length > 66) ? float.Parse(fields[66]) : 0.0f;
                        modelInput.Input67 = (fields.Length > 67) ? float.Parse(fields[67]) : 0.0f;
                        modelInput.Input68 = (fields.Length > 68) ? float.Parse(fields[68]) : 0.0f;
                        modelInput.Input69 = (fields.Length > 69) ? float.Parse(fields[69]) : 0.0f;
                        modelInput.Input70 = (fields.Length > 70) ? float.Parse(fields[70]) : 0.0f;
                        modelInput.Input71 = (fields.Length > 71) ? float.Parse(fields[71]) : 0.0f;
                        modelInput.Input72 = (fields.Length > 72) ? float.Parse(fields[72]) : 0.0f;
                        modelInput.Input73 = (fields.Length > 73) ? float.Parse(fields[73]) : 0.0f;
                        modelInput.Input74 = (fields.Length > 74) ? float.Parse(fields[74]) : 0.0f;
                        modelInput.Input75 = (fields.Length > 75) ? float.Parse(fields[75]) : 0.0f;
                        modelInput.Input76 = (fields.Length > 76) ? float.Parse(fields[76]) : 0.0f;
                        modelInput.Input77 = (fields.Length > 77) ? float.Parse(fields[77]) : 0.0f;
                        modelInput.Input78 = (fields.Length > 78) ? float.Parse(fields[78]) : 0.0f;
                        modelInput.Input79 = (fields.Length > 79) ? float.Parse(fields[79]) : 0.0f;
                        modelInput.Input80 = (fields.Length > 80) ? float.Parse(fields[80]) : 0.0f;
                        modelInput.Input81 = (fields.Length > 81) ? float.Parse(fields[81]) : 0.0f;
                        modelInput.Input82 = (fields.Length > 82) ? float.Parse(fields[82]) : 0.0f;
                        modelInput.Input83 = (fields.Length > 83) ? float.Parse(fields[83]) : 0.0f;
                        modelInput.Input84 = (fields.Length > 84) ? float.Parse(fields[84]) : 0.0f;
                        modelInput.Input85 = (fields.Length > 85) ? float.Parse(fields[85]) : 0.0f;
                        modelInput.Input86 = (fields.Length > 86) ? float.Parse(fields[86]) : 0.0f;
                        modelInput.Input87 = (fields.Length > 87) ? float.Parse(fields[87]) : 0.0f;
                        modelInput.Input88 = (fields.Length > 88) ? float.Parse(fields[88]) : 0.0f;
                        modelInput.Input89 = (fields.Length > 89) ? float.Parse(fields[89]) : 0.0f;
                        modelInput.Input90 = (fields.Length > 90) ? float.Parse(fields[90]) : 0.0f;
                        modelInput.Input91 = (fields.Length > 91) ? float.Parse(fields[91]) : 0.0f;
                        modelInput.Input92 = (fields.Length > 92) ? float.Parse(fields[92]) : 0.0f;
                        modelInput.Input93 = (fields.Length > 93) ? float.Parse(fields[93]) : 0.0f;
                        modelInput.Input94 = (fields.Length > 94) ? float.Parse(fields[94]) : 0.0f;
                        modelInput.Input95 = (fields.Length > 95) ? float.Parse(fields[95]) : 0.0f;
                        modelInput.Input96 = (fields.Length > 96) ? float.Parse(fields[96]) : 0.0f;
                        modelInput.Input97 = (fields.Length > 97) ? float.Parse(fields[97]) : 0.0f;
                        modelInput.Input98 = (fields.Length > 98) ? float.Parse(fields[98]) : 0.0f;
                        modelInput.Input99 = (fields.Length > 99) ? float.Parse(fields[99]) : 0.0f;
                        modelInput.Input100 = (fields.Length > 100) ? float.Parse(fields[100]) : 0.0f;

                        // keep this open it displays results

                        modelInputs.Add(modelInput);
                    }
                }
            }

            try
            {
                // 2) create List<ModelOutput> using PredictionEngine.Predict
                List<ModelOutput> modelOutputs = modelInputs.Select(x => predEngine.Predict(x)).ToList();

                // 3) write List<ModelOutput> to file
                var sb = new StringBuilder();
                Enumerable.Range(0, modelOutputs.Count).ToList().ForEach(x => sb.Append(modelInputs[x].Input1 + "," + modelInputs[x].Input2 + "," + modelOutputs[x].Prediction + "," + String.Join(",", modelOutputs[x].Score) + "\n"));
                var filePath3 = pathName + @"\output.csv";
                SaveUserData(filePath3, sb.ToString());

                //Console.WriteLine($"Single Prediction --> Actual value: {sampleData.Output} | Predicted value: {predictionResult.Prediction} | Predicted scores: [{String.Join(",", predictionResult.Score)}]");
            }
            catch (Exception x)
            {
                Console.WriteLine("Binary prediction failed: " + x.Message);
            }
        }

        private static void predictRegression(string pathName)
        {
            MLContext mlContext = new MLContext();

            var filePath1 = _baseDir + "\\" + pathName + @"\model.zip";
            ITransformer mlModel = mlContext.Model.Load(filePath1, out DataViewSchema inputSchema);
            var predEngine = mlContext.Model.CreatePredictionEngine<RegressionModelInput, ModelOutput>(mlModel);

            // 1) read test.csv into List<ModelInput>
            var modelInputs = new List<RegressionModelInput>();
            var filePath2 = pathName + @"\test.csv";
            var lines = LoadUserData(filePath2).Split('\n');
            if (lines.Length > 0)
            {
                var header = lines[0];
                var columnNames = header.Split(',');
                for (var ii = 1; ii < lines.Length; ii++)
                {
                    var line = lines[ii];
                    if (line.Length > 0)
                    {
                        var fields = line.Split(',');
                        var modelInput = new RegressionModelInput();
                        modelInput.Output = float.Parse(fields[0]);
                        modelInput.Input1 = fields[1]; // ticker
                        modelInput.Input2 = fields[2]; // interval;date

                        modelInput.Input3 = (fields.Length > 3) ? float.Parse(fields[3]) : 0.0f;
                        modelInput.Input4 = (fields.Length > 4) ? float.Parse(fields[4]) : 0.0f;
                        modelInput.Input5 = (fields.Length > 5) ? float.Parse(fields[5]) : 0.0f;
                        modelInput.Input6 = (fields.Length > 6) ? float.Parse(fields[6]) : 0.0f;
                        modelInput.Input7 = (fields.Length > 7) ? float.Parse(fields[7]) : 0.0f;
                        modelInput.Input8 = (fields.Length > 8) ? float.Parse(fields[8]) : 0.0f;
                        modelInput.Input9 = (fields.Length > 9) ? float.Parse(fields[9]) : 0.0f;
                        modelInput.Input10 = (fields.Length > 10) ? float.Parse(fields[10]) : 0.0f;
                        modelInput.Input11 = (fields.Length > 11) ? float.Parse(fields[11]) : 0.0f;
                        modelInput.Input12 = (fields.Length > 12) ? float.Parse(fields[12]) : 0.0f;
                        modelInput.Input13 = (fields.Length > 13) ? float.Parse(fields[13]) : 0.0f;
                        modelInput.Input14 = (fields.Length > 14) ? float.Parse(fields[14]) : 0.0f;
                        modelInput.Input15 = (fields.Length > 15) ? float.Parse(fields[15]) : 0.0f;
                        modelInput.Input16 = (fields.Length > 16) ? float.Parse(fields[16]) : 0.0f;
                        modelInput.Input17 = (fields.Length > 17) ? float.Parse(fields[17]) : 0.0f;
                        modelInput.Input18 = (fields.Length > 18) ? float.Parse(fields[18]) : 0.0f;
                        modelInput.Input19 = (fields.Length > 19) ? float.Parse(fields[19]) : 0.0f;
                        modelInput.Input20 = (fields.Length > 20) ? float.Parse(fields[20]) : 0.0f;
                        modelInput.Input21 = (fields.Length > 21) ? float.Parse(fields[21]) : 0.0f;
                        modelInput.Input22 = (fields.Length > 22) ? float.Parse(fields[22]) : 0.0f;
                        modelInput.Input23 = (fields.Length > 23) ? float.Parse(fields[23]) : 0.0f;
                        modelInput.Input24 = (fields.Length > 24) ? float.Parse(fields[24]) : 0.0f;
                        modelInput.Input25 = (fields.Length > 25) ? float.Parse(fields[25]) : 0.0f;
                        modelInput.Input26 = (fields.Length > 26) ? float.Parse(fields[26]) : 0.0f;
                        modelInput.Input27 = (fields.Length > 27) ? float.Parse(fields[27]) : 0.0f;
                        modelInput.Input28 = (fields.Length > 28) ? float.Parse(fields[28]) : 0.0f;
                        modelInput.Input29 = (fields.Length > 29) ? float.Parse(fields[29]) : 0.0f;
                        modelInput.Input30 = (fields.Length > 30) ? float.Parse(fields[30]) : 0.0f;
                        modelInput.Input31 = (fields.Length > 31) ? float.Parse(fields[31]) : 0.0f;
                        modelInput.Input32 = (fields.Length > 32) ? float.Parse(fields[32]) : 0.0f;
                        modelInput.Input33 = (fields.Length > 33) ? float.Parse(fields[33]) : 0.0f;
                        modelInput.Input34 = (fields.Length > 34) ? float.Parse(fields[34]) : 0.0f;
                        modelInput.Input35 = (fields.Length > 35) ? float.Parse(fields[35]) : 0.0f;
                        modelInput.Input36 = (fields.Length > 36) ? float.Parse(fields[36]) : 0.0f;
                        modelInput.Input37 = (fields.Length > 37) ? float.Parse(fields[37]) : 0.0f;
                        modelInput.Input38 = (fields.Length > 38) ? float.Parse(fields[38]) : 0.0f;
                        modelInput.Input39 = (fields.Length > 39) ? float.Parse(fields[39]) : 0.0f;
                        modelInput.Input40 = (fields.Length > 40) ? float.Parse(fields[40]) : 0.0f;
                        modelInput.Input41 = (fields.Length > 41) ? float.Parse(fields[41]) : 0.0f;
                        modelInput.Input42 = (fields.Length > 42) ? float.Parse(fields[42]) : 0.0f;
                        modelInput.Input43 = (fields.Length > 43) ? float.Parse(fields[43]) : 0.0f;
                        modelInput.Input44 = (fields.Length > 44) ? float.Parse(fields[44]) : 0.0f;
                        modelInput.Input45 = (fields.Length > 45) ? float.Parse(fields[45]) : 0.0f;
                        modelInput.Input46 = (fields.Length > 46) ? float.Parse(fields[46]) : 0.0f;
                        modelInput.Input47 = (fields.Length > 47) ? float.Parse(fields[47]) : 0.0f;
                        modelInput.Input48 = (fields.Length > 48) ? float.Parse(fields[48]) : 0.0f;
                        modelInput.Input49 = (fields.Length > 49) ? float.Parse(fields[49]) : 0.0f;
                        modelInput.Input50 = (fields.Length > 50) ? float.Parse(fields[50]) : 0.0f;
                        modelInput.Input51 = (fields.Length > 51) ? float.Parse(fields[51]) : 0.0f;
                        modelInput.Input52 = (fields.Length > 52) ? float.Parse(fields[52]) : 0.0f;
                        modelInput.Input53 = (fields.Length > 53) ? float.Parse(fields[53]) : 0.0f;
                        modelInput.Input54 = (fields.Length > 54) ? float.Parse(fields[54]) : 0.0f;
                        modelInput.Input55 = (fields.Length > 55) ? float.Parse(fields[55]) : 0.0f;
                        modelInput.Input56 = (fields.Length > 56) ? float.Parse(fields[56]) : 0.0f;
                        modelInput.Input57 = (fields.Length > 57) ? float.Parse(fields[57]) : 0.0f;
                        modelInput.Input58 = (fields.Length > 58) ? float.Parse(fields[58]) : 0.0f;
                        modelInput.Input59 = (fields.Length > 59) ? float.Parse(fields[59]) : 0.0f;
                        modelInput.Input60 = (fields.Length > 60) ? float.Parse(fields[60]) : 0.0f;
                        modelInput.Input61 = (fields.Length > 61) ? float.Parse(fields[61]) : 0.0f;
                        modelInput.Input62 = (fields.Length > 62) ? float.Parse(fields[62]) : 0.0f;
                        modelInput.Input63 = (fields.Length > 63) ? float.Parse(fields[63]) : 0.0f;
                        modelInput.Input64 = (fields.Length > 64) ? float.Parse(fields[64]) : 0.0f;
                        modelInput.Input65 = (fields.Length > 65) ? float.Parse(fields[65]) : 0.0f;
                        modelInput.Input66 = (fields.Length > 66) ? float.Parse(fields[66]) : 0.0f;
                        modelInput.Input67 = (fields.Length > 67) ? float.Parse(fields[67]) : 0.0f;
                        modelInput.Input68 = (fields.Length > 68) ? float.Parse(fields[68]) : 0.0f;
                        modelInput.Input69 = (fields.Length > 69) ? float.Parse(fields[69]) : 0.0f;
                        modelInput.Input70 = (fields.Length > 70) ? float.Parse(fields[70]) : 0.0f;
                        modelInput.Input71 = (fields.Length > 71) ? float.Parse(fields[71]) : 0.0f;
                        modelInput.Input72 = (fields.Length > 72) ? float.Parse(fields[72]) : 0.0f;
                        modelInput.Input73 = (fields.Length > 73) ? float.Parse(fields[73]) : 0.0f;
                        modelInput.Input74 = (fields.Length > 74) ? float.Parse(fields[74]) : 0.0f;
                        modelInput.Input75 = (fields.Length > 75) ? float.Parse(fields[75]) : 0.0f;
                        modelInput.Input76 = (fields.Length > 76) ? float.Parse(fields[76]) : 0.0f;
                        modelInput.Input77 = (fields.Length > 77) ? float.Parse(fields[77]) : 0.0f;
                        modelInput.Input78 = (fields.Length > 78) ? float.Parse(fields[78]) : 0.0f;
                        modelInput.Input79 = (fields.Length > 79) ? float.Parse(fields[79]) : 0.0f;
                        modelInput.Input80 = (fields.Length > 80) ? float.Parse(fields[80]) : 0.0f;
                        modelInput.Input81 = (fields.Length > 81) ? float.Parse(fields[81]) : 0.0f;
                        modelInput.Input82 = (fields.Length > 82) ? float.Parse(fields[82]) : 0.0f;
                        modelInput.Input83 = (fields.Length > 83) ? float.Parse(fields[83]) : 0.0f;
                        modelInput.Input84 = (fields.Length > 84) ? float.Parse(fields[84]) : 0.0f;
                        modelInput.Input85 = (fields.Length > 85) ? float.Parse(fields[85]) : 0.0f;
                        modelInput.Input86 = (fields.Length > 86) ? float.Parse(fields[86]) : 0.0f;
                        modelInput.Input87 = (fields.Length > 87) ? float.Parse(fields[87]) : 0.0f;
                        modelInput.Input88 = (fields.Length > 88) ? float.Parse(fields[88]) : 0.0f;
                        modelInput.Input89 = (fields.Length > 89) ? float.Parse(fields[89]) : 0.0f;
                        modelInput.Input90 = (fields.Length > 90) ? float.Parse(fields[90]) : 0.0f;
                        modelInput.Input91 = (fields.Length > 91) ? float.Parse(fields[91]) : 0.0f;
                        modelInput.Input92 = (fields.Length > 92) ? float.Parse(fields[92]) : 0.0f;
                        modelInput.Input93 = (fields.Length > 93) ? float.Parse(fields[93]) : 0.0f;
                        modelInput.Input94 = (fields.Length > 94) ? float.Parse(fields[94]) : 0.0f;
                        modelInput.Input95 = (fields.Length > 95) ? float.Parse(fields[95]) : 0.0f;
                        modelInput.Input96 = (fields.Length > 96) ? float.Parse(fields[96]) : 0.0f;
                        modelInput.Input97 = (fields.Length > 97) ? float.Parse(fields[97]) : 0.0f;
                        modelInput.Input98 = (fields.Length > 98) ? float.Parse(fields[98]) : 0.0f;
                        modelInput.Input99 = (fields.Length > 99) ? float.Parse(fields[99]) : 0.0f;
                        modelInput.Input100 = (fields.Length > 100) ? float.Parse(fields[100]) : 0.0f;

                        // keep this open it displays results

                        modelInputs.Add(modelInput);
                    }
                }
            }

            // 2) create List<ModelOutput> using PredictionEngine.Predict
            List<ModelOutput> modelOutputs = modelInputs.Select(x => predEngine.Predict(x)).ToList();

            // 3) write List<ModelOutput> to file
            var sb = new StringBuilder();
            Enumerable.Range(0, modelOutputs.Count).ToList().ForEach(x => sb.Append(modelInputs[x].Input1 + "," + modelInputs[x].Input2 + "," + modelOutputs[x].Prediction + "," + String.Join(",", modelOutputs[x].Score) + "\n"));
            var filePath3 = pathName + @"\output.csv";
            SaveUserData(filePath3, sb.ToString());

            //Console.WriteLine($"Single Prediction --> Actual value: {sampleData.Output} | Predicted value: {predictionResult.Prediction} | Predicted scores: [{String.Join(",", predictionResult.Score)}]");
        }


        private static void runRegressionExperiment(string pathName, string maxTime, string metric, string split, bool trained)
        {
            MLContext mlContext = new MLContext();

           // Console.WriteLine("Before getting training data.");

            // STEP 1: Load data
            var filePath2 = pathName + @"\train.csv";
            var text = LoadUserData(filePath2);
            var rows = text.Split('\n');
            int rowCount = rows.Length - 1;

            IDataView dataView = mlContext.Data.LoadFromTextFile<RegressionModelInput>(path: _baseDir + "\\" + pathName + @"\train.csv", hasHeader: true, separatorChar: ',', allowQuoting: true, allowSparse: false);

            var splitCount = (long)(rowCount * (int.Parse(split) / 100.0));
            var trainData = mlContext.Data.TakeRows(dataView, splitCount);
            var evaluateData = mlContext.Data.SkipRows(dataView, splitCount);

            var cols = rows[splitCount - 1].Split(',');
            var items = cols[2].Split(';');
           // Console.WriteLine("Last training date = " + items[1]);

           // Console.WriteLine("After getting training data.");

            var progressHandler = new RegressionExperimentProgressHandler();

            RegressionExperimentSettings experimentSettings = new RegressionExperimentSettings();
            experimentSettings.MaxExperimentTimeInSeconds = uint.Parse(maxTime);
            experimentSettings.OptimizingMetric = getRegressionOptimizingMetric(metric);
            ExperimentResult<RegressionMetrics> experimentResult = null;


            try
            {
                //Console.WriteLine("Before experiment.");
                var time1 = DateTime.Now;

                experimentResult = mlContext.Auto().CreateRegressionExperiment(experimentSettings).Execute(trainData, evaluateData, progressHandler: progressHandler, labelColumnName: "output");

                //Console.WriteLine("After experiment. Duration = " + (DateTime.Now - time1).TotalSeconds);
            }
            catch (Exception x)
            {
                //done = false;

                var message = x.ToString();

                Console.WriteLine(message);

            }

           // Console.WriteLine("After training.");

            ITransformer trainedModel = null;

            if (experimentResult != null)
            {
                // Print top models found by AutoML
                bool ok = PrintTopRegressionModels(pathName, experimentResult, metric, trained);

                // STEP 4: Evaluate the model using test data and print metrics
                RunDetail<RegressionMetrics> bestRun = experimentResult.BestRun;

                if (ok && bestRun != null)
                {
                    trainedModel = bestRun.Model;

                    if (trainedModel != null)
                    {
                        //Console.WriteLine("Before saving top model.");

                        // STEP 5: Save/persist the trained model to a .ZIP file

                        var filePath = _baseDir + "\\" + pathName + @"\model.zip";
                        File.Delete(filePath);
                        mlContext.Model.Save(trainedModel, dataView.Schema, filePath);

                        //Console.WriteLine("The model is saved to {0}", @"C:\Users\ATM\Documents\ATMML\ATMML\senarios\" + senario  + @"\" + interval + @"\model.zip");

                        //Console.WriteLine("After saving top model.");
                    }
                }
            }
            else
            {
                Console.WriteLine("COULD NOT TRAIN ANY MODELS!");
                Console.WriteLine(pathName);
            }

        }

        private static void runBinaryExperiment(string pathName, string maxTime, string metric, string split, bool trained)
        {

            MLContext mlContext = new MLContext();

           // Console.WriteLine("Before getting training data.");

            // STEP 1: Load data
            var filePath2 = pathName + @"\train.csv";
            var text = LoadUserData(filePath2);
            var rows = text.Split('\n');
            int rowCount = rows.Length - 1;

            IDataView dataView = mlContext.Data.LoadFromTextFile<BinaryModelInput>(path: _baseDir + "\\" + pathName + @"\train.csv", hasHeader: true, separatorChar: ',', allowQuoting: true, allowSparse: false);

            var splitCount = (long)(rowCount * (int.Parse(split) / 100.0));
            var trainData = mlContext.Data.TakeRows(dataView, splitCount);
            var evaluateData = mlContext.Data.SkipRows(dataView, splitCount);

            var cols = rows[splitCount - 1].Split(',');
            var items = cols[2].Split(';');
            //Console.WriteLine("Last training date = " + items[1]);

            //Console.WriteLine("After getting training data.");
            // STEP 2: Initialize our user-defined progress handler that AutoML will 
            // invoke after each model it produces and evaluates.

            BinaryExperimentSettings settings = new BinaryExperimentSettings();
            settings.MaxExperimentTimeInSeconds = uint.Parse(maxTime);
            settings.OptimizingMetric = getBinaryOptimizingMetric(metric);

            settings.Trainers.Remove(BinaryClassificationTrainer.LightGbm);
            //settings.Trainers.Remove(BinaryClassificationTrainer.SymbolicSgdLogisticRegression);
            //settings.Trainers.Remove(BinaryClassificationTrainer.LinearSvm);

            //settings.Trainers.Remove(BinaryClassificationTrainer.AveragedPerceptron);
            //settings.Trainers.Remove(BinaryClassificationTrainer.SgdCalibrated);
            //settings.Trainers.Remove(BinaryClassificationTrainer.SdcaLogisticRegression);
            //settings.Trainers.Remove(BinaryClassificationTrainer.FastForest);
            //settings.Trainers.Remove(BinaryClassificationTrainer.FastTree);
            //settings.Trainers.Remove(BinaryClassificationTrainer.LbfgsLogisticRegression);


            var progressHandler = new BinaryExperimentProgressHandler();
            //var bcts = Enum.GetValues(typeof(BinaryClassificationTrainer)).Cast<BinaryClassificationTrainer>().ToList();
            ExperimentResult<BinaryClassificationMetrics> experimentResult = null;

            try
            {
               // Console.WriteLine("Before experiment.");
                var time1 = DateTime.Now;

                experimentResult = mlContext.Auto().CreateBinaryClassificationExperiment(settings).Execute(trainData, evaluateData, progressHandler: progressHandler, labelColumnName: "output");

                //Console.WriteLine("After experiment. Duration = " + (DateTime.Now - time1).TotalSeconds);
            }
            catch (Exception x)
            {
                //done = false;

                var message = x.ToString();

                //Console.WriteLine(message);

            }

           // Console.WriteLine("After training.");

            ITransformer trainedModel = null;

            if (experimentResult != null)
            {
                // Print top models found by AutoML
                bool ok = PrintTopBinaryModels(pathName, experimentResult, metric, trained);

                // STEP 4: Evaluate the model using test data and print metrics
                RunDetail<BinaryClassificationMetrics> bestRun = experimentResult.BestRun;

                if (ok && bestRun != null)
                {
                    trainedModel = bestRun.Model;

                    if (trainedModel != null)
                    {
                        //Console.WriteLine("Before saving top model.");

                        // STEP 5: Save/persist the trained model to a .ZIP file

                        var filePath = _baseDir + "\\" + pathName + @"\model.zip";
                        File.Delete(filePath);
                        mlContext.Model.Save(trainedModel, dataView.Schema, filePath);

                        //Console.WriteLine("The model is saved to {0}", @"C:\Users\ATM\Documents\ATMML\ATMML\senarios\" + senario  + @"\" + interval + @"\model.zip");

                       // Console.WriteLine("After saving top model.");
                    }
                }
            }
            else
            {
                Console.WriteLine("COULD NOT TRAIN ANY MODELS!");
                Console.WriteLine(pathName);
            }
        }

        private static BinaryClassificationMetric getBinaryOptimizingMetric(string metric)
        {
            var output = BinaryClassificationMetric.Accuracy;
            if (metric == "AUPRC") output = BinaryClassificationMetric.AreaUnderPrecisionRecallCurve;
            else if (metric == "AUC") output = BinaryClassificationMetric.AreaUnderRocCurve;
            else if (metric == "F1-SCORE") output = BinaryClassificationMetric.F1Score;
            return output;
        }

        private static RegressionMetric getRegressionOptimizingMetric(string metric)
        {
            var output = RegressionMetric.RSquared;
            if (metric == "MAE") output = RegressionMetric.MeanAbsoluteError;
            else if (metric == "MSE") output = RegressionMetric.MeanSquaredError;
            else if (metric == "RMSE") output = RegressionMetric.RootMeanSquaredError;
            return output;
        }

        private static double getBinarySortMetric(BinaryClassificationMetrics input, string metric)
        {
            var output = input.Accuracy;
            if (metric == "AUPRC") output = input.AreaUnderPrecisionRecallCurve;
            else if (metric == "AUC") output = input.AreaUnderRocCurve;
            else if (metric == "F1-SCORE") output = input.F1Score;
            return output;
        }

        private static double getRegressionSortMetric(RegressionMetrics input, string metric)
        {
            var output = input.RSquared;
            if (metric == "MAE") output = input.MeanAbsoluteError;
            else if (metric == "MSE") output = input.MeanSquaredError;
            else if (metric == "RMSE") output = input.RootMeanSquaredError;
            return output;
        }

        private static bool PrintTopRegressionModels(string pathName, ExperimentResult<RegressionMetrics> experimentResult, string metric, bool trained)
        {
            var ok = true;

            var sortMetric = getRegressionOptimizingMetric(metric);

            // Get top few runs ranked by selected metric
            var topRuns = experimentResult.RunDetails
                .Where(r => r.ValidationMetrics != null && !double.IsNaN(getRegressionSortMetric(r.ValidationMetrics, metric)))
                .OrderByDescending(r => getRegressionSortMetric(r.ValidationMetrics, metric));

            Console.WriteLine("Top models for " + metric + " count = " + topRuns.Count());

            var oldMetric = double.NaN;
            if (trained)
            {
                Console.WriteLine("Check if old model is better!");
                try
                {
                    var filePath1 = pathName + @"\result.csv";
                    var lines = LoadUserData(filePath1).Split('\n');
                    if (lines.Length > 0)
                    {
                        var line = lines[0];
                        if (line.Length > 0)
                        {
                            var models = line.Split(':');
                            if (models.Length > 0)
                            {
                                var metrics = models[0].Replace("%", "").Split(',');
                                if (metrics.Length > 4)
                                {
                                    if (metric == "MAE") oldMetric = (metrics[3] == "NaN") ? 0 : double.Parse(metrics[2]);
                                    else if (metric == "MRE") oldMetric = (metrics[2] == "NaN") ? 0 : double.Parse(metrics[3]);
                                    else if (metric == "RMSE") oldMetric = (metrics[4] == "NaN") ? 0 : double.Parse(metrics[4]);
                                    else oldMetric = (metrics[1] == "NaN") ? 0 : double.Parse(metrics[1]);
                                }
                            }
                        }
                    }
                    Console.WriteLine("Old metric = " + oldMetric);
                }
                catch (Exception x)
                {
                    Console.WriteLine(x.Message);
                }

                if (topRuns.Count() > 0 && !double.IsNaN(oldMetric))
                {
                    var run = topRuns.ElementAt(0);
                    var name = run.TrainerName;
                    var metrics = run.ValidationMetrics;

                    double newMetric = double.NaN;
                    if (metric == "MAE") newMetric = 100 * metrics.MeanAbsoluteError;
                    else if (metric == "MSE") newMetric = 100 * metrics.MeanSquaredError;
                    else if (metric == "RMSE") newMetric = 100 * metrics.RootMeanSquaredError;
                    else newMetric = 100 * metrics.RSquared;
                    if (!double.IsNaN(newMetric) && oldMetric >= newMetric)
                    {
                        Console.WriteLine("Old model is the same or better! " + oldMetric + " " + newMetric);
                        ok = false;
                    }
                    else
                    {
                        Console.WriteLine("New model is better! " + oldMetric + " " + newMetric);
                    }
                }
            }

            if (ok)
            {
                Console.WriteLine("Before saving top models results");

                var sb = new StringBuilder();
                for (var i = 0; i < topRuns.Count(); i++)
                {
                    var run = topRuns.ElementAt(i);
                    //ConsoleHelper.PrintIterationMetrics(i + 1, run.TrainerName, run.ValidationMetrics, run.RuntimeInSeconds);
                    // ConsoleHelper.PrintBinaryClassificationMetrics(run.TrainerName, run.ValidationMetrics);

                    var name = run.TrainerName;
                    var metrics = run.ValidationMetrics;
                    var seperator = (i == 0) ? "" : ":";
                    sb.Append(seperator + name + "," + $"{metrics.RSquared:P2}" + "," + $"{metrics.MeanAbsoluteError:P2}" + "," + $"{metrics.MeanSquaredError:P2}" + "," + $"{metrics.RootMeanSquaredError:P2}" + "\n");
                }
                var filePath = pathName + @"\result.csv";
                SaveUserData(filePath, sb.ToString());

                Console.WriteLine("After saving top models results");
            }
            return ok;
        }

        /// <summary>
        /// Prints top models from AutoML experiment.
        /// </summary>
        private static bool PrintTopBinaryModels(string pathName, ExperimentResult<BinaryClassificationMetrics> experimentResult, string metric, bool trained)
        {
            var ok = true;

            var sortMetric = getBinaryOptimizingMetric(metric);

            // Get top few runs ranked by selected metric
            var topRuns = experimentResult.RunDetails
                .Where(r => r.ValidationMetrics != null && !double.IsNaN(getBinarySortMetric(r.ValidationMetrics, metric)))
                .OrderByDescending(r => getBinarySortMetric(r.ValidationMetrics, metric));

            Console.WriteLine("Top models for " + metric + " count = " + topRuns.Count());

            //ConsoleHelper.PrintBinaryClassificationMetricsHeader();

            var oldMetric = double.NaN;
            if (trained)
            {
                Console.WriteLine("Check if old model is better!");
                try
                {
                    var filePath1 = pathName + @"\result.csv";
                    var lines = LoadUserData(filePath1).Split('\n');
                    if (lines.Length > 0)
                    {
                        var line = lines[0];
                        if (line.Length > 0)
                        {
                            var models = line.Split(':');
                            if (models.Length > 0)
                            {
                                var metrics = models[0].Replace("%", "").Split(',');
                                if (metrics.Length > 4)
                                {
                                    if (metric == "AUPRC") oldMetric = (metrics[3] == "NaN") ? 0 : double.Parse(metrics[3]);
                                    else if (metric == "AUC") oldMetric = (metrics[2] == "NaN") ? 0 : double.Parse(metrics[2]);
                                    else if (metric == "F1-SCORE") oldMetric = (metrics[4] == "NaN") ? 0 : double.Parse(metrics[4]);
                                    else oldMetric = (metrics[1] == "NaN") ? 0 : double.Parse(metrics[1]);
                                }
                            }
                        }
                    }
                    Console.WriteLine("Old metric = " + oldMetric);
                }
                catch (Exception x)
                {
                    Console.WriteLine(x.Message);
                }

                if (topRuns.Count() > 0 && !double.IsNaN(oldMetric))
                {
                    var run = topRuns.ElementAt(0);
                    var name = run.TrainerName;
                    var metrics = run.ValidationMetrics;

                    double newMetric = double.NaN;
                    if (metric == "AUPRC") newMetric = 100 * metrics.AreaUnderPrecisionRecallCurve;
                    else if (metric == "AUC") newMetric = 100 * metrics.AreaUnderRocCurve;
                    else if (metric == "F1-SCORE") newMetric = 100 * metrics.F1Score;
                    else newMetric = 100 * metrics.Accuracy;
                    if (!double.IsNaN(newMetric) && oldMetric >= newMetric)
                    {
                        Console.WriteLine("Old model is the same or better! " + oldMetric + " " + newMetric);
                        ok = false;
                    }
                    else
                    {
                        Console.WriteLine("New model is better! " + oldMetric + " " + newMetric);
                    }
                }
            }

            if (ok)
            {
                Console.WriteLine("Before saving top models results");

                var sb = new StringBuilder();
                for (var i = 0; i < topRuns.Count(); i++)
                {
                    var run = topRuns.ElementAt(i);
                    //ConsoleHelper.PrintIterationMetrics(i + 1, run.TrainerName, run.ValidationMetrics, run.RuntimeInSeconds);
                    // ConsoleHelper.PrintBinaryClassificationMetrics(run.TrainerName, run.ValidationMetrics);

                    var name = run.TrainerName;
                    var metrics = run.ValidationMetrics;
                    var seperator = (i == 0) ? "" : ":";
                    sb.Append(seperator + name + "," + $"{metrics.Accuracy:P2}" + "," + $"{metrics.AreaUnderRocCurve:P2}" + "," + $"{metrics.AreaUnderPrecisionRecallCurve:P2}" + "," + $"{metrics.F1Score:P2}" + "\n");
                }
                var filePath = pathName + @"\result.csv";
                SaveUserData(filePath, sb.ToString());
                var time = DateTime.Now.ToString("MMM d H:mm");
                SaveUserData(pathName + @"\time", time);

                Console.WriteLine("After saving top models results");
            }
            var lastRun = DateTime.Now.ToString("MMM d H:mm");
            SaveUserData(pathName + @"\lastRun", lastRun);
            return ok;
        }
    }
}
