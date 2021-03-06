using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.IO;//必須

//下の2つを参照に追加する必要がある
//"C:\Program Files (x86)\Windows Kits\8.1\References\CommonConfiguration\Neutral\Annotated\Windows.winmd"
//"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore\v4.5\System.Runtime.WindowsRuntime.dll"

//参照したところ
//WPFアプリにPDFを表示する — 某エンジニアのお仕事以外のメモ（分冊）
//https://water2litter.net/rum/post/cs_pdf_wpf/

//    [UWP][PDF] PDFファイルを表示する | HIROs.NET Blog
//http://blog.hiros-dot.net/?p=7346


//    (C#)Parallel.For, Parallel.ForEach並列処理の挙動確認 - Qiita
//https://qiita.com/longlongago_k/items/8f19d84fce6dd677922e
//マルチスレッドがわからん


namespace PDFtoGAZO
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private Windows.Data.Pdf.PdfDocument MyPdfDocument;//PDFファイルを読み込んだもの
        private string MyPdfPath;//読み込んだPDFファイルのフルパス
        private string MyPdfDirectory;//読み込んだPDFファイルのフォルダ
        private string MyPdfName;//読み込んだPDFファイル名
        private double MyDpi;//PDFを画像に変換する時のDPI

        public MainWindow()
        {
            InitializeComponent();

            this.AllowDrop = true;
            this.Drop += MainWindow_Drop;

            MyPdfPath = @"D:\ブログ用\1708_04.pdf";
            MyPdfPath = @"M:\小説ラノベ\test\Neorude 2 (Manual)(JP)(PlayStation)(PSX).pdf";
            MyDpi = 96;


            LoadPdf(MyPdfPath);

        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) == false) { return; }

            string[] filePath = (string[])e.Data.GetData(DataFormats.FileDrop);
            LoadPdf(filePath[0]);
        }

        private async void LoadPdf(string filePath)
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
            try
            {
                MyPdfDocument = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);
                MyPdfDirectory = System.IO.Path.GetDirectoryName(filePath);
                MyPdfName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                MyDpi = 96;
                DisplayImage(0, 96);
                tbPageCount.Text = $"ページ数 : {MyPdfDocument.PageCount.ToString()}";
            }
            catch (Exception)
            { }
        }




        private async void DisplayImage(int pageIndex, double dpi)
        {
            if (MyPdfDocument == null) { return; }
            MyDpi = dpi;
            using (Windows.Data.Pdf.PdfPage page = MyPdfDocument.GetPage((uint)pageIndex))
            {
                double h = page.Size.Height;
                var options = new Windows.Data.Pdf.PdfPageRenderOptions();
                options.DestinationHeight = (uint)Math.Round(page.Size.Height * (dpi / 96.0), MidpointRounding.AwayFromZero);
                tbDpi.Text = $"dpi : {dpi.ToString()}";
                tbHeight.Text = $"縦ピクセル : {options.DestinationHeight.ToString()}";

                using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                {
                    await page.RenderToStreamAsync(stream, options);

                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream.AsStream();//using System.IOがないとエラーになる
                    image.EndInit();
                    MyImage.Source = image;
                    MyImage.Width = image.PixelWidth;
                    MyImage.Height = image.PixelHeight;
                }
            }
        }


        private void ButtonDpi96_Click(object sender, RoutedEventArgs e)
        {
            DisplayImage(0, 96);
        }

        private void ButtonDpi150_Click(object sender, RoutedEventArgs e)
        {
            DisplayImage(0, 150);
        }

        private void ButtonDpi300_Click(object sender, RoutedEventArgs e)
        {
            DisplayImage(0, 300);
        }

        private void ButtonDpi600_Click(object sender, RoutedEventArgs e)
        {
            DisplayImage(0, 600);
        }


        private async void SaveSub(double dpi, string directory, string fileName, int pageIndex, int quality, int keta)
        {
            using (Windows.Data.Pdf.PdfPage page = MyPdfDocument.GetPage((uint)pageIndex))
            {
                //指定されたdpiを元に画像サイズ指定、四捨五入                
                var options = new Windows.Data.Pdf.PdfPageRenderOptions();
                options.DestinationHeight = (uint)Math.Round(page.Size.Height * (dpi / 96.0), MidpointRounding.AwayFromZero);

                using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                {
                    await page.RenderToStreamAsync(stream, options);

                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream.AsStream();
                    image.EndInit();

                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.QualityLevel = quality;
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    pageIndex++;
                    string renban = pageIndex.ToString("d" + keta);
                    using (var fileStream = new FileStream(
                        System.IO.Path.Combine(directory, fileName) + "_" + renban + ".jpg", FileMode.Create, FileAccess.Write))
                    {
                        encoder.Save(fileStream);
                    }
                }
            }
        }


        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (MyPdfDocument == null) { return; }
            int keta = MyPdfDocument.PageCount.ToString().Length;//0埋め連番の桁数

            for (int i = 0; i < MyPdfDocument.PageCount; i++)
            {
                SaveSub(MyDpi, MyPdfDirectory, MyPdfName, i, 85, keta);
            }
            MessageBox.Show("ok");

        }







        /// <summary>
        /// jpeg画像で保存
        /// </summary>
        /// <param name="dpi">PDFファイルを読み込む時のDPI</param>
        /// <param name="directory">保存フォルダ</param>
        /// <param name="fileName">保存名</param>
        /// <param name="pageIndex">保存するPDFのページ</param>
        /// <param name="quality">jpegの品質min0、max100</param>
        /// <param name="keta">保存名につける連番0埋めの桁数</param>
        /// <returns></returns>
        private async Task SaveSub2(double dpi, string directory, string fileName, int pageIndex, int quality, int keta)
        {
            using (Windows.Data.Pdf.PdfPage page = MyPdfDocument.GetPage((uint)pageIndex))
            {
                //指定されたdpiを元に画像サイズ指定、四捨五入                
                var options = new Windows.Data.Pdf.PdfPageRenderOptions();
                options.DestinationHeight = (uint)Math.Round(page.Size.Height * (dpi / 96.0), MidpointRounding.AwayFromZero);

                using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                {
                    await page.RenderToStreamAsync(stream, options);

                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream.AsStream();
                    image.EndInit();

                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.QualityLevel = quality;
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    pageIndex++;
                    string renban = pageIndex.ToString("d" + keta);
                    using (var fileStream = new FileStream(
                        System.IO.Path.Combine(directory, fileName) + "_" + renban + ".jpg", FileMode.Create, FileAccess.Write))
                    {
                        encoder.Save(fileStream);
                    }
                }
            }

        }

        private async void Button2_Click(object sender, RoutedEventArgs e)
        {
            if (MyPdfDocument == null) { return; }

            this.IsEnabled = false;
            try
            {
                int keta = MyPdfDocument.PageCount.ToString().Length;//0埋め連番の桁数

                //各ページの保存処理のタスクのリスト作成
                var MyTasks = new List<Task>();
                for (int i = 0; i < MyPdfDocument.PageCount; i++)
                {
                    //MyTasks.Add(SaveSub2(MyDpi, MyPdfDirectory, MyPdfName, i, 85, keta));
                }

                //各タスク実行
                for (int i = 0; i < MyTasks.Count; i++)
                {
                    //await MyTasks[i];//今まで、シングルスレッド
                    //MyTasks[i].RunSynchronously();//エラー出るけど処理される
                    //MyTasks[i];//エラー
                    //_ = MyTasks[i];//処理前にループを抜けてしまう
                    //var neko = MyTasks[i];//処理前にループを抜けてしまう
                    //Task.WaitAll(MyTasks[i]);//デッドロック
                    //MyTasks[i].Start();//エラー出るけど処理される


                }

                List<BitmapImage> imgList = new List<BitmapImage>();
                List<Task<BitmapImage>> imgTaskList = new List<Task<BitmapImage>>();
                for (int i = 0; i < MyPdfDocument.PageCount; i++)
                {
                    //imgList.Add(RenderPage(85, i));//エラーになる
                    imgTaskList.Add(RenderPageAsync(88, i));//Taskのリストが作成されるだけで実行はされない
                    //imgList.Add(await RenderPageAsync(88, i));//シングルスレッドOK
                }
                foreach (var item in imgTaskList)
                {
                    item.Start();
                }
                BitmapImage neko;
                System.Collections.Concurrent.ConcurrentBag<BitmapImage> inu = new System.Collections.Concurrent.ConcurrentBag<BitmapImage>();
                Parallel.For(0, MyPdfDocument.PageCount, async i =>
                 {
                     //inu.Add(await RenderPageAsync(88,(int) i));//別スレッドが所有しているエラー
                     //neko = await RenderPageAsync(88, (int)i);//別スレッドが所有しているエラー
                 });

                //BitmapImage neko = await RenderPageAsync(88, 0);
                //BitmapImage inu = await RenderPageAsync(88, 1);//.ConfigureAwait(false);
                //BitmapImage uma = await RenderPageAsync(88, 2);
                //BitmapImage tako = await RenderPageAsync(88, 3);
                //BitmapImage ika = await RenderPageAsync(88, 4);
                //BitmapImage uni = await RenderPageAsync(88, 5);
                //uni = await RenderPageAsync(88, 6);
                //uni = await RenderPageAsync(88, 7);
                //uni = await RenderPageAsync(88, 8);
                //uni = await RenderPageAsync(88, 9);
                //uni = await RenderPageAsync(88, 10);

                MessageBox.Show("処理完了");
            }
            catch (Exception ex) { MessageBox.Show($"なんかエラー出たわ \n {ex.Message} \n {ex.ToString()}"); }

            finally { this.IsEnabled = true; }

        }

        private async Task<BitmapImage> RenderPageAsync(double dpi, int pageIndex)
        {
            BitmapImage image = new BitmapImage();
            using (Windows.Data.Pdf.PdfPage page = MyPdfDocument.GetPage((uint)pageIndex))
            {
                //指定されたdpiを元に画像サイズ指定、四捨五入                
                var options = new Windows.Data.Pdf.PdfPageRenderOptions();
                options.DestinationHeight = (uint)Math.Round(page.Size.Height * (dpi / 96.0), MidpointRounding.AwayFromZero);

                using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                {
                    await page.RenderToStreamAsync(stream, options);

                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream.AsStream();
                    image.EndInit();
                }
            }
            return image;
        }

        private BitmapImage RenderPage(double dpi, int pageIndex)
        {
            BitmapImage image = new BitmapImage();
            using (Windows.Data.Pdf.PdfPage page = MyPdfDocument.GetPage((uint)pageIndex))
            {
                //指定されたdpiを元に画像サイズ指定、四捨五入                
                var options = new Windows.Data.Pdf.PdfPageRenderOptions();
                options.DestinationHeight = (uint)Math.Round(page.Size.Height * (dpi / 96.0), MidpointRounding.AwayFromZero);

                using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                {
                    page.RenderToStreamAsync(stream, options);

                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream.AsStream();
                    image.EndInit();
                }
            }
            return image;
        }

        private List<Windows.Data.Pdf.PdfPage> GetPages()
        {
            List<Windows.Data.Pdf.PdfPage> pages = new List<Windows.Data.Pdf.PdfPage>();
            //for (uint i = 0; i < MyPdfDocument.PageCount; i++)
            //{
            //    using (var neko = MyPdfDocument.GetPage(i))
            //    {
            //        pages.Add(neko);
            //    }
            //}

            Parallel.For(0, MyPdfDocument.PageCount, i =>
           {
               using (var neko = MyPdfDocument.GetPage((uint)i))
               {
                   pages.Add(neko);
               }
           });

            return pages;
        }

        private async void ButtonTest1_Click(object sender, RoutedEventArgs e)
        {
            //await Task.Run(() => WorkerThread1());
            //Func<string> func = () => { return WorkerThread1(); };
            //string s = func();

            var pages = GetPages();
            Task<BitmapImage> image = MakeImage(pages[0]);
            var neko = await image;
            var inu = image.Result;

            //List<BitmapImage> ii = new List<BitmapImage>();
            //for (int i = 0; i < pages.Count; i++)
            //{
            //    ii.Add(await MakeImage(pages[i]));
            //}

            System.Collections.Concurrent.ConcurrentBag<BitmapImage> bag = new System.Collections.Concurrent.ConcurrentBag<BitmapImage>();
            Parallel.For(0, pages.Count, async i =>
             {
                 bag.Add(await MakeImage(pages[i]));//エラー、別スレッドが所有している
             });
        }

        private string WorkerThread1()
        {
            var pages = GetPages();
            //var neko = pages[0];
            //var taskTest2 = Test2();
            //var inu= await streams;//list<image>

            //List<BitmapImage> images = new List<BitmapImage>();
            //Task<List<BitmapImage>> tasks;
            //List<BitmapImage> bitmapImages = new List<BitmapImage>();
            //Parallel.Invoke(async () => { bitmapImages = await taskTest2; });
            //var inu= bitmapImages[0];

            Parallel.For(0, pages.Count, async i =>
            {
                //Windows.Data.Pdf.PdfPage page = pages[i];

                using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                {
                    await pages[i].RenderToStreamAsync(stream);
                }

                //using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                //{
                //    await Task.Run(async () =>
                //    {                          
                //        await pages[i].RenderToStreamAsync(stream);
                //    });
                //}

                //await Task.Run(async () =>
                //{
                //    using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                //    {
                //        await pages[i].RenderToStreamAsync(stream);
                //    }
                //});
            });
            return "終了";
        }


        private async Task<List<BitmapImage>> Test2()
        {
            var streams = new List<Windows.Storage.Streams.InMemoryRandomAccessStream>();
            var images = new List<BitmapImage>();
            for (uint i = 0; i < MyPdfDocument.PageCount; i++)
            {
                using (var neko = MyPdfDocument.GetPage(i))
                {
                    using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                    {
                        await neko.RenderToStreamAsync(stream);
                        streams.Add(stream);
                        var img = new BitmapImage();
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.StreamSource = stream.AsStream();
                        img.EndInit();
                        images.Add(img);
                    }
                }
            }
            return images;
        }

        private async Task<BitmapImage> MakeImage(Windows.Data.Pdf.PdfPage page)
        {
            var img = new BitmapImage();
            using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
            {
                await page.RenderToStreamAsync(stream);
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = stream.AsStream();
                img.EndInit();
            }
            return img;
        }

        //全ページBitmapSourceのリスト
        private async void ButtonTest2_Click(object sender, RoutedEventArgs e)
        {
            List<BitmapFrame> frames = new List<BitmapFrame>();
            var ps = GetPages();
            for (int i = 0; i < ps.Count; i++)
            {
                using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                {
                    await ps[i].RenderToStreamAsync(stream);
                    frames.Add(BitmapFrame.Create(stream.AsStream()));

                }
            }
        }

        private async void ButtonTest3_Click(object sender, RoutedEventArgs e)
        {

            int cpuCount = Environment.ProcessorCount;
            //System.Collections.Concurrent.ConcurrentBag<BitmapFrame> frames = new System.Collections.Concurrent.ConcurrentBag<BitmapFrame>();
            var frames = new List<BitmapFrame>();
            List<Windows.Data.Pdf.PdfPage> ps = GetPages();
            int windowSize = ps.Count / cpuCount;

            var ff = new BitmapFrame[ps.Count];
            var tt = new Task[ps.Count];

            //IEnumerable<Windows.Foundation.IAsyncAction> pp = ps.Select(n => n.RenderToStreamAsync(stream));
            var tb = new List<Task<BitmapImage>>();
            for (int i = 0; i < ps.Count; i++)
            {
                tb.Add(MakeImage(ps[i]));
            }
            //BitmapImage[] ko = await Task.WhenAll(tb).ConfigureAwait(true);
            BitmapImage[] ko =await Task.WhenAll(tb).ConfigureAwait(false);
            var neko = ko[1];

            var bb = new List<BitmapImage>();
            
            //for (int i = 0; i < ps.Count; i++)
            //{
            //    bb.Add(await tb[i].ConfigureAwait(true));//ok
            //    //bb.Add(tb[i].Result);//デッドロックになる
            //    //BitmapImage ii = tb[i].Result;
            //}
            //var inu = bb[1];

            var cc = new BitmapImage[ps.Count];
            Parallel.For(0, ps.Count,async n =>
             {
                 //cc[n] =  tb[n].Result;//デッドロックになる
                 cc[n] =await tb[n].ConfigureAwait(false);
             });
            var inuu = cc[1];
            
            //for (int n = 0; n < ps.Count; n++)
            //{
            //    using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
            //    {
            //        await ps[n].RenderToStreamAsync(stream);
            //        //tt[n] = ps[n].RenderToStreamAsync(stream).AsTask();
            //        ff[n] = BitmapFrame.Create(stream.AsStream());
            //    }
            //}

            //await Task.WhenAll(Enumerable.Range(0, cpuCount).Select(n => Task.Run(async () =>
            // {
            //     for (int i = n * windowSize; i < (n + 1) * windowSize; i++)
            //     {
            //         using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
            //         {
            //             await ps[i].RenderToStreamAsync(stream);
            //             frames.Add(BitmapFrame.Create(stream.AsStream()));
            //         }
            //     }
            // })));


        }
        
        //未検証、ページから画像作成から保存までの処理をTaskにする、あとでTaskWhenAllで実行？
        private async Task SaveTaskAsync(Windows.Data.Pdf.PdfPage page, string directory, string fileName, int pageIndex, int quality, int keta)
        {
            using (var stream=new Windows.Storage.Streams.InMemoryRandomAccessStream())
            {
               await page.RenderToStreamAsync(stream);
                var encoder = new JpegBitmapEncoder();
                encoder.QualityLevel = quality;
                encoder.Frames.Add(BitmapFrame.Create(stream.AsStream()));
                string renban = pageIndex.ToString("d" + keta);
                using (var fileStream = new FileStream(
                    System.IO.Path.Combine(directory, fileName) + "_" + renban + ".jpg", FileMode.Create, FileAccess.Write))
                {
                    encoder.Save(fileStream);
                }
            }
        }
    }
}
