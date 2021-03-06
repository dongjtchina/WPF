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
using Windows.Data.Pdf;
using System.IO.Compression;


//下の2つを参照に追加する必要がある
//"C:\Program Files (x86)\Windows Kits\8.1\References\CommonConfiguration\Neutral\Annotated\Windows.winmd"
//"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore\v4.5\System.Runtime.WindowsRuntime.dll"

//参照したところ
//WPFアプリにPDFを表示する — 某エンジニアのお仕事以外のメモ（分冊）
//https://water2litter.net/rum/post/cs_pdf_wpf/

//    [UWP][PDF] PDFファイルを表示する | HIROs.NET Blog
//http://blog.hiros-dot.net/?p=7346

//C# Taskの待ちかた集 - Qiita
//https://qiita.com/takutoy/items/d45aa736ced25a8158b3

//    Windows 8.1の新機能、PDFを表示するには？［Windows 8.1ストア・アプリ開発］：WinRT／Metro TIPS - ＠IT
//https://www.atmarkit.co.jp/ait/articles/1310/24/news070.html





namespace PDFtoJPEG
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private PdfDocument MyPdfDocument;//PDFファイルを読み込んだもの
        //private string MyPdfPath;//読み込んだPDFファイルのフルパス
        private string MyPdfDirectory;//読み込んだPDFファイルのフォルダ
        private string MyPdfName;//読み込んだPDFファイル名
        private double MyDpi;//PDFを画像に変換する時のDPI

        public MainWindow()
        {
            InitializeComponent();

            this.Title = "PDFtoJPEG";
            this.AllowDrop = true;
            this.Drop += MainWindow_Drop;

            //左クリックで元画像とプレビュー画像の切り替え
            MyScrollViewer.PreviewMouseLeftButtonDown += (s, e) => { Panel.SetZIndex(MyImage, 1); };
            MyScrollViewer.PreviewMouseLeftButtonUp += (s, e) => { Panel.SetZIndex(MyImage, -1); };

        }




        //ファイルがドロップされたとき
        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) == false) { return; }

            string[] filePath = (string[])e.Data.GetData(DataFormats.FileDrop);
            LoadPdf(filePath[0]);
        }

        //PDFファイルを読み込んで最初のページを表示
        private async void LoadPdf(string filePath)
        {

            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
            
            try
            {
                using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
                {

                    MyPdfDocument = await PdfDocument.LoadFromStreamAsync(stream);
                    MyPdfDirectory = System.IO.Path.GetDirectoryName(filePath);
                    MyPdfName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                    MyDpi = 96;
                    DisplayImage(0, 96);//表示
                    NumePageIndex.Value = 1;

                    var pageCount = MyPdfDocument.PageCount;
                    tbPageCount.Text = $"{pageCount.ToString()} ページ";
                    NumePageIndex.Max = (int)pageCount;

                    long fileSize = new FileInfo(filePath).Length;
                    tbOriginFileSize.Text = $"元pdf {GetFixFileSize(fileSize)}";

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"開くことができなかった、PDFファイルじゃないかも \n \n {ex.ToString()}");
            }
        }



        //PDFファイルを画像に変換して表示
        private async void DisplayImage(int pageIndex, double dpi)
        {
            if (MyPdfDocument == null) { return; }

            int c = (int)MyPdfDocument.PageCount - 1;
            if (pageIndex > c)
            {
                pageIndex = c;
            }
            if (pageIndex < 0) pageIndex = 0;

            MyDpi = dpi;
            using (PdfPage page = MyPdfDocument.GetPage((uint)pageIndex))
            {
                //作成する画像の縦ピクセル数を指定されたdpiから決める
                var options = new PdfPageRenderOptions();
                options.DestinationHeight = (uint)Math.Round(page.Size.Height * (dpi / 96.0), MidpointRounding.AwayFromZero);
                options.DestinationWidth = (uint)Math.Round(page.Size.Width * (dpi / 96.0), MidpointRounding.AwayFromZero);
                tbDpi.Text = $"{dpi.ToString()} dpi";
                tbHeight.Text = $"縦{options.DestinationHeight.ToString()} px";
                tbWidth.Text = $"横{options.DestinationWidth.ToString()} px";

                //画像に変換
                using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                {
                    await page.RenderToStreamAsync(stream, options);//画像に変換はstreamへ

                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream.AsStream();//using System.IOがないとエラーになる
                    image.EndInit();
                    MyImage.Source = image;
                    MyImage.Width = image.PixelWidth;
                    MyImage.Height = image.PixelHeight;
                    //プレビュー用のjpeg画像表示
                    MyImagePreviwer.Source = MakeJpegPreviewImage(image, GetJpegQualityFix());
                    UpdateDisplayPngSize(image);//pngサイズ表示更新
                }
            }
        }

        //プレビュー用のjpeg画像作成は
        //BitmapSourceをEncoderでstreamにSaveして、それをDecoderで取得？する
        private BitmapSource MakeJpegPreviewImage(BitmapSource source, int quality)
        {
            if (source == null) { return null; }

            var encoder = new JpegBitmapEncoder();
            JpegBitmapDecoder decoder;
            encoder.QualityLevel = quality;
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (var stream = new MemoryStream())
            {
                //jpeg画像をSaveしてから取り出す
                encoder.Save(stream);
                stream.Seek(0, SeekOrigin.Begin);
                decoder = new JpegBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                //このときのstreamのlengthがjpeg画像のサイズになるみたいなので取得、表示
                tbFileSize.Text = $"jpg {GetFixFileSize(stream.Length)}";
                tbJpegAllSize.Text = $"jpg {GetFixFileSize(stream.Length * MyPdfDocument.PageCount)}";

            }
            return decoder.Frames[0];
        }

        //予想ファイルサイズで使う、単位をMBかKBにする、1000KB以上ならMBにする
        private string GetFixFileSize(double size)
        {
            double fileSize = size / 1000.0;
            if (fileSize > 1000)
            {
                return $"{(fileSize / 1000.0).ToString(".0")} MB";
            }
            else
            {
                return $"{fileSize.ToString("0")} KB";
            }
        }

        //Pngの予想ファイルサイズ
        private void UpdateDisplayPngSize(BitmapSource source)
        {
            if (source == null) return;

            var encoder = new PngBitmapEncoder();
            PngBitmapDecoder decoder;
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                stream.Seek(0, SeekOrigin.Begin);
                decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                //表示更新
                tbPngSize.Text = $"png {GetFixFileSize(stream.Length)}";
                tbPngAllSize.Text = $"png {GetFixFileSize(stream.Length * MyPdfDocument.PageCount)}";
            }
        }




        //DPI指定ボタンクリック時
        //-1しているのはPDFのページは0から数えるけど、見る方は1から数えるから

        private void ButtonDpi48_Click(object sender, RoutedEventArgs e)
        {
            DisplayImage(NumePageIndex.Value - 1, 48);
        }
        private void ButtonDpi96_Click(object sender, RoutedEventArgs e)
        {
            DisplayImage(NumePageIndex.Value - 1, 96);
        }

        private void ButtonDpi150_Click(object sender, RoutedEventArgs e)
        {
            DisplayImage(NumePageIndex.Value - 1, 150);
        }

        private void ButtonDpi300_Click(object sender, RoutedEventArgs e)
        {
            DisplayImage(NumePageIndex.Value - 1, 300);
        }

        private void ButtonDpi600_Click(object sender, RoutedEventArgs e)
        {
            DisplayImage(NumePageIndex.Value - 1, 600);
        }

        //プレビュー画像の更新
        private void ButtonPreviweUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (MyImage.Source == null) { return; }
            DisplayImage(NumePageIndex.Value - 1, MyDpi);
        }






        /// <summary>
        /// jpeg画像で保存
        /// </summary>
        private async Task SaveSub2_1(int pageIndex, int keta, bool isJpeg)
        {
            var encoder = MakeEncoder(isJpeg);
            using (PdfPage page = MyPdfDocument.GetPage((uint)pageIndex))
            {
                string ext = isJpeg ? ".jpg" : ".png";
                //指定されたdpiを元に画像サイズ指定、四捨五入                
                var options = new PdfPageRenderOptions();
                options.DestinationHeight = (uint)Math.Round(page.Size.Height * (MyDpi / 96.0), MidpointRounding.AwayFromZero);

                using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                {
                    await page.RenderToStreamAsync(stream, options);//画像に変換したのはstreamへ

                    //streamから直接BitmapFrameを作成することができた                    
                    encoder.Frames.Add(BitmapFrame.Create(stream.AsStream()));
                    //連番ファイル名を作成して保存
                    pageIndex++;
                    string renban = pageIndex.ToString("d" + keta);

                    using (var fileStream = new FileStream(
                        System.IO.Path.Combine(MyPdfDirectory, MyPdfName) + "_" + renban + ext, FileMode.Create, FileAccess.Write))
                    {
                        encoder.Save(fileStream);
                    }

                }
            }

        }

        private async void ButtonSaveJpeg_Click(object sender, RoutedEventArgs e)
        {
            await SaveJpegPng(true);
        }
        private async void ButtonSavePng_Click(object sender, RoutedEventArgs e)
        {
            await SaveJpegPng(false);
        }

        private async Task SaveJpegPng(bool isJpeg)
        {
            if (MyPdfDocument == null) { return; }

            this.IsEnabled = false;


            if (CheckBoxZip.IsChecked == false)
            {
                try
                {
                    int keta = MyPdfDocument.PageCount.ToString().Length;//0埋め連番の桁数

                    //各ページの保存処理のリスト作成
                    var MyTasks = new List<Task>();
                    for (int i = 0; i < MyPdfDocument.PageCount; i++)
                    {
                        MyTasks.Add(SaveSub2_1(i, keta, isJpeg));
                    }

                    //各タスク実行
                    for (int i = 0; i < MyTasks.Count; i++)
                    {
                        await MyTasks[i];
                    }

                    MessageBox.Show("処理完了");
                }
                catch (Exception ex) { MessageBox.Show($"なんかエラー出たわ \n {ex.Message} \n {ex.ToString()}"); }

                finally { this.IsEnabled = true; }
            }
            //zipで
            else
            {
                try
                {
                    int keta = MyPdfDocument.PageCount.ToString().Length;//0埋め連番の桁数
                    await SaveSub3_1(MyPdfDocument, MyDpi, MyPdfDirectory, MyPdfName, GetJpegQualityFix(), keta, isJpeg);
                    //await Save5(MyPdfDocument, MakeZipArchive(MyPdfDirectory, MyPdfName), keta, 85, 96);
                    MessageBox.Show("処理完了");
                }
                catch (Exception ex) { MessageBox.Show($"なんかエラー出たわ \n {ex.Message} \n {ex.ToString()}"); }

                finally { this.IsEnabled = true; }
            }
        }

        private BitmapEncoder MakeEncoder(bool isJpeg)
        {
            if (isJpeg)
            {
                JpegBitmapEncoder j = new JpegBitmapEncoder();
                j.QualityLevel = GetJpegQualityFix();
                return j;
            }
            else
            {
                return new PngBitmapEncoder();
            }
        }

        //ユーザーコントロールとして作ったnumericupdownがバグっているので修正した数値を渡す
        private int GetJpegQualityFix()
        {
            int quality = NumeJpegQuality.Value;
            if (quality < NumeJpegQuality.Min) { quality = NumeJpegQuality.Min; }
            if (quality > NumeJpegQuality.Max) { quality = NumeJpegQuality.Max; }
            return quality;
        }

        /// <summary>
        /// PdfDocumentをjpegにして、1つのzipファイルにする
        /// </summary>
        /// <param name="pdfDocument">Windows.Data.Pdf</param>
        /// <param name="dpi">PDFを画像にするときに使う</param>
        /// <param name="directory">保存フォルダ</param>
        /// <param name="fileName">zipファイル名とjpegファイル名に使う</param>
        /// <param name="quality">jpeg品質</param>
        /// <param name="keta">0埋め連番の桁数、jpegファイル名に使う</param>
        /// <returns></returns>      
        private async Task SaveSub3_1(PdfDocument pdfDocument, double dpi, string directory, string fileName, int quality, int keta, bool isJpeg)
        {
            string exte = isJpeg ? ".jpg" : ".png";
            BitmapEncoder encoder;
            string zipName = System.IO.Path.Combine(directory, fileName) + ".zip";
            using (var zipstream = File.Create(zipName))
            {
                using (ZipArchive archive = new ZipArchive(zipstream, ZipArchiveMode.Create))
                {

                    for (int i = 0; i < MyPdfDocument.PageCount; i++)
                    {
                        int renban = i + 1;
                        string jpegName = MyPdfName + "_" + renban.ToString("d" + keta) + exte;
                        if (isJpeg)
                        {
                            JpegBitmapEncoder j = new JpegBitmapEncoder();
                            j.QualityLevel = quality;
                            encoder = j;
                        }
                        else { encoder = new PngBitmapEncoder(); }

                        using (PdfPage page = pdfDocument.GetPage((uint)i))
                        {
                            var options = new PdfPageRenderOptions();
                            options.DestinationHeight = (uint)Math.Round(page.Size.Height * (dpi / 96.0), MidpointRounding.AwayFromZero);

                            using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                            {
                                await page.RenderToStreamAsync(stream, options);//画像に変換したのはstreamへ                                
                                encoder.Frames.Add(BitmapFrame.Create(stream.AsStream()));
                                var entry = archive.CreateEntry(jpegName);
                                //open
                                using (var entryStream = entry.Open())
                                {
                                    using (var jpegStream = new MemoryStream())
                                    {
                                        encoder.Save(jpegStream);
                                        jpegStream.Position = 0;
                                        jpegStream.CopyTo(entryStream);
                                    }
                                }
                            }
                        }
                    }//for
                }
            }
        }









        #region 失敗
        private async Task Save6()
        {
            var archive = MakeZipArchive(MyPdfDirectory, MyPdfName);
            int keta = MyPdfDocument.PageCount.ToString().Length;//0埋め連番の桁数
            await Save5(MyPdfDocument, archive, keta, 85, 96);
        }
        private ZipArchive MakeZipArchive(string directory, string fileName)
        {
            string zipName = System.IO.Path.Combine(directory, fileName) + ".zip";
            using (var ms = new MemoryStream())
            {
                using (var zipstream = File.Create(zipName))
                {
                    using (ZipArchive archive = new ZipArchive(zipstream, ZipArchiveMode.Create))
                    {
                        return archive;
                    }
                }
            }
        }

        private async Task Save5(PdfDocument pdfDocument, ZipArchive archive, int keta, int quality, double dpi)
        {
            for (int i = 0; i < MyPdfDocument.PageCount; i++)
            {
                int renban = i + 1;
                string jpegName = MyPdfName + "_" + renban.ToString("d" + keta) + ".jpg";

                using (PdfPage page = pdfDocument.GetPage((uint)i))
                {
                    var options = new PdfPageRenderOptions();
                    options.DestinationHeight = (uint)Math.Round(page.Size.Height * (dpi / 96.0), MidpointRounding.AwayFromZero);

                    using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                    {
                        await page.RenderToStreamAsync(stream, options);//画像に変換したのはstreamへ
                        JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                        encoder.QualityLevel = quality;
                        encoder.Frames.Add(BitmapFrame.Create(stream.AsStream()));

                        var entry = archive.CreateEntry(jpegName);
                        //open
                        using (var entryStream = entry.Open())
                        {
                            using (var jpegStream = new MemoryStream())
                            {
                                encoder.Save(jpegStream);
                                jpegStream.Position = 0;
                                jpegStream.CopyTo(entryStream);
                            }
                        }
                    }
                }
            }
        }
        #endregion

  
    }
}
