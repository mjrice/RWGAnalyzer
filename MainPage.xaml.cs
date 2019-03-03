using System;
using System.Xml;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.ViewManagement;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RWGAnalyzer
{
    /// <summary>
    /// A
    /// </summary>
    public sealed partial class MainPage : Page
    {
        string worldFolder = "";
        
        class itemType
        {
            public string name;
            public int count;
            public int type;
        };

        public class ConsoleClassType
        {
            public enum ResultClass { summary, prefabList, prefabMap,water, nowhere };
            public TextBlock textBoxSummary;
            public TextBlock textBoxPrefabMap;
            public TextBlock textBoxPrefabList;

            public void setTextBox(ResultClass resultclass,TextBlock tb)
            {
                if (resultclass == ResultClass.summary)
                    textBoxSummary = tb;
                else if (resultclass == ResultClass.prefabList)
                    textBoxPrefabList = tb;
                else if (resultclass == ResultClass.prefabMap)
                    textBoxPrefabMap = tb;
            }

            public void WriteLine(string ln, ResultClass resultclass = ResultClass.nowhere)
            {
                Write(ln, resultclass);
                Write("\n", resultclass);
            }

            public void Write(string txt, ResultClass resultclass = ResultClass.nowhere)
            {
                if (resultclass == ResultClass.summary)                
                    textBoxSummary.Text += txt;                
                else if (resultclass == ResultClass.prefabList)
                   textBoxPrefabList.Text += txt;
                else if (resultclass == ResultClass.prefabMap)
                    textBoxPrefabMap.Text += txt;
            }

            public void ClearAll()
            {
                textBoxPrefabList.Text = "";
                textBoxPrefabMap.Text = "";
                textBoxSummary.Text = "";
            }

        };

        ConsoleClassType Console;
        Windows.Storage.StorageFile biomesMapFile;

        public MainPage()
        {
            ApplicationView.PreferredLaunchViewSize = new Size(1630, 850);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
            this.InitializeComponent();
            Console = new ConsoleClassType();
            
        }

        private async void GetFolder()
        {
            FolderPicker folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");
            Windows.Storage.StorageFolder folder = await folderPicker.PickSingleFolderAsync();

            if (folder != null)
            {
                // Application now has read/write access to all contents in the picked folder
                // (including other sub-folder contents)
                Windows.Storage.AccessCache.StorageApplicationPermissions.
                FutureAccessList.AddOrReplace("PickedFolderToken", folder);
                worldFolder = folder.Path;
                Console.ClearAll();
                biomesMapFile = await folder.GetFileAsync("biomes.png");

            }            
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
           Console.setTextBox(ConsoleClassType.ResultClass.summary, TextBlockSummary);
           Console.setTextBox(ConsoleClassType.ResultClass.prefabList, TextBlockPrefabList);
           Console.setTextBox(ConsoleClassType.ResultClass.prefabMap, TextBlockPrefabMap);
           
           worldFolder = "";
           GetFolder();
           while (worldFolder=="") await Task.Delay(100);
           Analyze();
        }

        private async void Analyze()
        {            
            List<itemType> namesList = new List<itemType>();
            List<string> traderList = new List<string>();
            bool isknown;
            int counts = 0;
            string fpfn;
            char[] charSeparators = new char[] { ',' };
            string[] result;
            int xSize = 8192;
            int zSize = 8192;
                                    
            using (Windows.Storage.Streams.IRandomAccessStream fileStream =
            await biomesMapFile.OpenAsync(Windows.Storage.FileAccessMode.Read))
            {
                // Set the image source to the selected bitmap.
                Windows.UI.Xaml.Media.Imaging.BitmapImage bitmapImage = new Windows.UI.Xaml.Media.Imaging.BitmapImage();
                bitmapImage.SetSource(fileStream);
                BiomesImage.Source = bitmapImage;
            }

            //
            // load all the xml documents we need to analyze
            //
            Console.WriteLine("Reading world info from " + worldFolder);

            XmlDocument xinfoDoc = new XmlDocument();
            XmlDocument xdoc = new XmlDocument();
            XmlDocument xwaterdoc = new XmlDocument();

            fpfn = worldFolder + "\\map_info.xml";            
            await Task.Run(() => xinfoDoc.Load(new FileStream(fpfn, FileMode.Open)) );

            string fpfn2 = worldFolder + "\\prefabs.xml";
            await Task.Run(() => xdoc.Load(new FileStream(fpfn2, FileMode.Open)) );      

            string fpfn3 = worldFolder + "\\water_info.xml";
            await Task.Run(() => xwaterdoc.Load(new FileStream(fpfn3, FileMode.Open)) );

            await Task.Delay(50);

            SummaryHeading.Text = "World Analysis for: " + worldFolder;
            //Console.WriteLine("World: " + worldFolder, ConsoleClassType.ResultClass.summary);

            foreach (XmlNode node in xinfoDoc.DocumentElement.ChildNodes)
            {
                string nextName = node.Attributes["name"].Value;
                if (nextName.Equals("HeightMapSize"))
                {
                    string size = node.Attributes["value"].Value;
                    result = size.Split(charSeparators, StringSplitOptions.None);
                    xSize = Int16.Parse(result[0]);
                    zSize = Int16.Parse(result[1]);
                    Console.WriteLine("The world size is " + xSize + " by " + zSize,ConsoleClassType.ResultClass.summary);
                }
            }

            int[] typecounts = new int[13];
            string[] typenames = new string[13];
            typenames[0] = "other";
            typenames[1] = "trader";
            typenames[2] = "survivor_site";
            typenames[3] = "skyscraper";
            typenames[4] = "junkyard";
            typenames[5] = "house";
            typenames[6] = "utility";
            typenames[7] = "store";
            typenames[8] = "cabin";
            typenames[9] = "waste_bldg";
            typenames[10] = "cave";
            typenames[11] = "factory";
            typenames[12] = "field";

            string[] typenames_ignore = new string[2];
            typenames_ignore[0] = "sign";
            typenames_ignore[1] = "street_light";

            Console.WriteLine("processing input file...");

            int maxXLocation = -100;
            int minXLocation = 100;
            int maxZLocation = -100;
            int minZLocation = 100;
            int maxYLocation = -100;
            int minYLocation = 100;

            // on the first pass, just determining how many of each type of prefab exist
            counts = 0;
            foreach (XmlNode node in xdoc.DocumentElement.ChildNodes)
            {
                // each node should appear like this:  <decoration type="model" name="fastfood_01" position="214,49,-2856" rotation="1" />
                counts++;
                string nextName = node.Attributes["name"].Value;
                string location = node.Attributes["position"].Value;

                isknown = false;

                foreach (string dontcare in typenames_ignore)
                {
                    if (nextName.Contains(dontcare))
                        isknown = true;
                }

                if (isknown == false) foreach (itemType item in namesList)
                    {
                        if (item.name.Equals(nextName))
                        {
                            isknown = true;
                            item.count++;
                        }
                    }
                if (isknown == false)
                {
                    itemType newitem = new itemType();
                    newitem.name = nextName;
                    newitem.count = 1;
                    newitem.type = 0;
                    for (int k = 1; k < typenames.Length; k++)
                    {
                        if (typenames[k].Length > 0)
                            if (nextName.Contains(typenames[k])) newitem.type = k;
                    }
                    namesList.Add(newitem);
                }

                result = location.Split(charSeparators, StringSplitOptions.None);
                int xLocation = Int16.Parse(result[0]);
                int yLocation = Int16.Parse(result[1]);
                int zLocation = Int16.Parse(result[2]);

                if (nextName.Contains("trader"))
                {
                    traderList.Add(location);
                }

                if (xLocation > maxXLocation) maxXLocation = xLocation;
                if (xLocation < minXLocation) minXLocation = xLocation;

                if (yLocation > maxYLocation) maxYLocation = yLocation;
                if (yLocation < minYLocation) minYLocation = yLocation;

                if (zLocation > maxZLocation) maxZLocation = zLocation;
                if (zLocation < minZLocation) minZLocation = zLocation;
            }


            // divide the world into a matrix of 10 by 10 blocks and then tally which block each prefab is in, in order to see how spread out or clumpy it is
            float widthX = xSize * .1f; //(float)(maxXLocation-minXLocation+1) * .1f;
            float widthZ = zSize * .1f; //(float)(maxZLocation-minZLocation+1) * .1f;
            int[,] bins = new int[10, 10];
            foreach (XmlNode node in xdoc.DocumentElement.ChildNodes)
            {
                string location = node.Attributes["position"].Value;
                result = location.Split(charSeparators, StringSplitOptions.None);
                int xLocation = Int16.Parse(result[0]);
                int zLocation = Int16.Parse(result[2]);

                float xbin = (float)(xLocation - minXLocation) / widthX;
                int xbinInt = (int)(xbin);

                float zbin = (float)(zLocation - minZLocation) / widthZ;
                int zbinInt = (int)(zbin);

                bins[xbinInt, zbinInt] += 1;
            }

            Console.WriteLine("There were " + counts + " prefab instances of " + namesList.Count + " types found in this world.",ConsoleClassType.ResultClass.summary );
            Console.WriteLine("Prefabs will spawn between coordinates (" + minXLocation + "," + minYLocation + "," + minZLocation + ") and (" + maxXLocation + "," + maxYLocation + "," + maxZLocation + ")", ConsoleClassType.ResultClass.summary);

            Console.WriteLine("In a grid with each block " + widthX + " by " + widthZ + " meters, here is the number of prefabs in each block:",ConsoleClassType.ResultClass.prefabMap);

            for (int z = 9; z >= 0; z--)
            {
                Console.Write("\t", ConsoleClassType.ResultClass.prefabMap);
                for (int x = 0; x < 10; x++)
                {
                    Console.Write(bins[x, z] + "\t", ConsoleClassType.ResultClass.prefabMap);
                }
                Console.Write("\n", ConsoleClassType.ResultClass.prefabMap);
            }

            // sort the list of prefabs (most common ones first)
            namesList.Sort((x, y) => y.count.CompareTo(x.count));

            for (int i = 0; i < typecounts.Length; i++) typecounts[i] = 0;

            // tally count of each prefab type found
            foreach (itemType item in namesList)
            {
                typecounts[item.type] += item.count;
            }

            int mostcommoncount = 0;
            for (int i = 0; i < 10; i++)
            {
                mostcommoncount += namesList[i].count;
            }

            float pct;
            pct = (float)mostcommoncount / counts * 10000.0f;
            int pcti = (int)pct;
            pct = pcti * .01f;

            Console.WriteLine("The 10 most commonly duplicated prefabs account for " + pct + "% of the prefabs and are: ",ConsoleClassType.ResultClass.prefabList);
            Console.WriteLine("\tPrefab:              \tOccurances:");
            for (int i = 0; i < 10; i++)
            {
                Console.WriteLine("\t" + namesList[i].name.PadRight(21) + "\t" + namesList[i].count, ConsoleClassType.ResultClass.prefabList);
            }

            Console.WriteLine("\nSummary of prefab types:", ConsoleClassType.ResultClass.prefabList);
            for (int i = 0; i < typecounts.Length; i++)
            {
                pct = (float)typecounts[i] / counts * 10000.0f;
                pcti = (int)pct;
                pct = pcti * .01f;
                Console.WriteLine("\t" + typenames[i].PadRight(13) + " = " + typecounts[i].ToString().PadLeft(4) + " (" + pct + "%)", ConsoleClassType.ResultClass.prefabList);
            }

            // analyze trader locations
            double aveDistance = 0.0;
            int countdistances = 0;
            foreach (string position in traderList)
            {
                result = position.Split(charSeparators, StringSplitOptions.None);
                int x1 = Int16.Parse(result[0]);
                int z1 = Int16.Parse(result[2]);
                double nearestNeighborDistance = 10000.0;

                foreach (string position2 in traderList)
                {
                    if (position.Equals(position2) == false)
                    {
                        result = position2.Split(charSeparators, StringSplitOptions.None);
                        int x2 = Int16.Parse(result[0]);
                        int z2 = Int16.Parse(result[2]);

                        double distance = Math.Sqrt((x2 - x1) * (x2 - x1) + (z2 - z1) * (z2 - z1));
                        if (distance < nearestNeighborDistance) nearestNeighborDistance = distance;
                    }
                }
                aveDistance += nearestNeighborDistance;
                countdistances++;
            }
            aveDistance /= (double)countdistances;
            aveDistance = (int)(aveDistance + .5);
            Console.WriteLine("\nOn average, each trader is " + aveDistance + " meters away from another trader.",ConsoleClassType.ResultClass.summary);

            Console.WriteLine("\nAnalyzing bodies of water, this may take a minute...");
            // analyze water
            // <WaterSources>
            // <Water pos="-4092, 39, -3484" minx="-4096" maxx="4096" minz="-4096" maxz="4096"/>
            // The water blocks appear to be 8 meters by 8 meters each
            int waterBlockSize = 8;
            int waterblockcount = 0;
            int buildings_in_water = 0;
            foreach (XmlNode node in xwaterdoc.DocumentElement.ChildNodes)
            {
                waterblockcount++;
                string waterPos = node.Attributes["pos"].Value;
                result = waterPos.Split(charSeparators, StringSplitOptions.None);
                int waterX = Int16.Parse(result[0]);
                int waterZ = Int16.Parse(result[2]);

                //Console.WriteLine("next water block is at " + waterX + ", " + waterZ);

                /* this doesn't work right
                 foreach(XmlNode poinode in xdoc.DocumentElement.ChildNodes)
                 {
                    string location = poinode.Attributes["position"].Value;
                    result = location.Split(charSeparators, StringSplitOptions.None);
                    int xLocation = Int16.Parse(result[0]);
                    int zLocation = Int16.Parse(result[2]);
                    if(Math.Abs(xLocation-waterX)<waterBlockSize/2 && Math.Abs(zLocation-waterZ)<waterBlockSize/2)
                    {
                        buildings_in_water++;
                        break;
                    }
                } */
            }

            int problems = 0;
            double worldSquareMeters = xSize * zSize;
            double waterArea = (waterblockcount * waterBlockSize * waterBlockSize);
            double pctWater = waterArea / worldSquareMeters;
            pctWater *= 10000.0;
            pctWater = (int)pctWater;
            pctWater *= .01;
            Console.WriteLine("\nIdentified " + waterArea + " square meters of water, which is " + pctWater + "% of the land area.", ConsoleClassType.ResultClass.prefabMap);
            //Console.WriteLine("It appears that there are " + buildings_in_water + " buildings located in the water.");

            int[,] waterbins = new int[10, 10];
            foreach (XmlNode node in xwaterdoc.DocumentElement.ChildNodes)
            {
                string location = node.Attributes["pos"].Value;
                result = location.Split(charSeparators, StringSplitOptions.None);
                int xLocation = Int16.Parse(result[0]);
                int zLocation = Int16.Parse(result[2]);

                float xbin = (float)(xLocation - minXLocation) / widthX;
                int xbinInt = (int)(xbin);

                float zbin = (float)(zLocation - minZLocation) / widthZ;
                int zbinInt = (int)(zbin);

                if (xbinInt < 0 || xbinInt > 9 || zbinInt < 0 || zbinInt > 9)
                    problems++;
                else
                    waterbins[xbinInt, zbinInt] += 1;
            }

            Console.WriteLine("In a grid with each block " + widthX + " by " + widthZ + " meters, here is the percentage of each grid block that is covered in water:", ConsoleClassType.ResultClass.prefabMap);
            for (int z = 9; z >= 0; z--)
            {
                Console.Write("\t", ConsoleClassType.ResultClass.prefabMap);
                for (int x = 0; x < 10; x++)
                {
                    float waterarea = waterbins[x, z] * waterBlockSize * waterBlockSize;
                    pct = waterarea / (widthX * widthZ) * 1000.0f;
                    pct = (int)pct;
                    pct *= 0.1f;
                    string s = pct.ToString("N1");
                    Console.Write(s.PadLeft(4) + "%\t", ConsoleClassType.ResultClass.prefabMap);
                }
                Console.Write("\n", ConsoleClassType.ResultClass.prefabMap);
            }
        }
    }
}

