//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using SDKTemplate;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Tesseract;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using Page = Windows.UI.Xaml.Controls.Page;
using Rect = Windows.Foundation.Rect;

namespace SDKTemplate
{
    public sealed partial class OcrFileImage : Page
    {
        // A pointer back to the main page.
        // This is needed if you want to call methods in MainPage such as NotifyUser()
        private MainPage rootPage = MainPage.Current;

        // Bitmap holder of currently loaded image.
        private SoftwareBitmap bitmap;

        // Recognized words overlay boxes.
        private List<WordOverlay> wordBoxes = new List<WordOverlay>();

        private IReadOnlyList<ComboBoxItem> tesseractLanguages = [
            new ComboBoxItem() { Content = "FRA" },
            new ComboBoxItem() { Content = "DEU" },
            new ComboBoxItem() { Content = "ITA" },
            new ComboBoxItem() { Content = "ENG" }
        ];

        public OcrFileImage()
        {
            InitializeComponent();
        }
         
        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// Updates OCR available languages and loads sample image.
        /// </summary>
        /// <param name="e"></param>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            UpdateAvailableLanguages();

            await LoadSampleImageAsync();
        }

        /// <summary>
        /// Updates list of ocr languages available on device.
        /// </summary>
        private void UpdateAvailableLanguages()
        {
            if (OcrEngineList.SelectedIndex == -1)
            {
                return;
            }

            if (UserLanguageToggle.IsOn)
            {
                switch (OcrEngineList.SelectedIndex)
                {
                    // Tesseract
                    case 0:
                        LanguageList.SelectedIndex = 0;
                        LanguageList.IsEnabled = false;
                        rootPage.NotifyUser(
                            "Run OCR in French.",
                            NotifyType.StatusMessage);
                        break;

                    case 1: // Microsoft
                        LanguageList.ItemsSource = null;
                        LanguageList.IsEnabled = false;

                        rootPage.NotifyUser(
                            "Run OCR in first OCR available language from UserProfile.GlobalizationPreferences.Languages list.",
                            NotifyType.StatusMessage);
                        break;

                    case 2: // Azure
                    default:// Another
                        // Prevent OCR if no OCR languages are available on device.
                        //UserLanguageToggle.IsEnabled = false;
                        LanguageList.IsEnabled = false;
                        ExtractButton.IsEnabled = false;

                        rootPage.NotifyUser("No available OCR languages.", NotifyType.ErrorMessage);
                        break;
                }
            }
            else
            {
                // If 'Auto Detect' is not enabled, update available languages for OCR engine based on selected engine.
                switch (OcrEngineList.SelectedIndex)
                {
                    // Tesseract
                    case 0:
                        // Check if any Tesseract OCR language is available on device.
                        LanguageList.DisplayMemberPath = "DisplayName";
                        LanguageList.ItemsSource = tesseractLanguages;
                        LanguageList.SelectedIndex = 0;
                        LanguageList.IsEnabled = true;
                        break;

                    // Microsoft
                    case 1:
                        // Check if any Microsoft OCR language is available on device.
                        if (OcrEngine.AvailableRecognizerLanguages.Count > 0)
                        {
                            LanguageList.DisplayMemberPath = "DisplayName";
                            LanguageList.ItemsSource = OcrEngine.AvailableRecognizerLanguages;
                            LanguageList.SelectedIndex = 0;
                            LanguageList.IsEnabled = true;
                        }
                        break;

                    // Azure
                    case 2:
                    // Another
                    default:
                        // Prevent OCR if no OCR languages are available on device.
                        //UserLanguageToggle.IsEnabled = false;
                        LanguageList.IsEnabled = false;
                        ExtractButton.IsEnabled = false;

                        rootPage.NotifyUser("No available OCR languages.", NotifyType.ErrorMessage);
                        break;
                }

            }
        }

        /// <summary>
        /// Loads image from file to bitmap and displays it in UI.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private async Task LoadImageAsync(StorageFile file)
        {
            using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
            {
                var decoder = await BitmapDecoder.CreateAsync(stream);

                bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                var imgSource = new WriteableBitmap(bitmap.PixelWidth, bitmap.PixelHeight);
                bitmap.CopyToBuffer(imgSource.PixelBuffer);
                PreviewImage.Source = imgSource;
            }
        }

        private async Task LoadSampleImageAsync()
        {
            var file = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync("Assets\\splash-sdk.png");
            await LoadImageAsync(file);
        }

        /// <summary>
        /// Clears extracted text and text overlay from previous OCR.
        /// </summary>
        private void ClearResults()
        {
            TextOverlay.RenderTransform = null;
            ExtractedTextBox.Text = String.Empty;
            TextOverlay.Children.Clear();
            wordBoxes.Clear();
        }

        /// <summary>
        ///  Update word box transform to match current UI size.
        /// </summary>
        private void UpdateWordBoxTransform()
        {
            // Used for text overlay.
            // Prepare scale transform for words since image is not displayed in original size.
            ScaleTransform scaleTrasform = new ScaleTransform
            {
                CenterX = 0,
                CenterY = 0,
                ScaleX = PreviewImage.ActualWidth / bitmap.PixelWidth,
                ScaleY = PreviewImage.ActualHeight / bitmap.PixelHeight
            };

            foreach (var item in wordBoxes)
            {
                item.Transform(scaleTrasform);
            }
        }
        public async Task<byte[]> SoftwareBitmapToByteArrayAsync(SoftwareBitmap softwareBitmap)
        {
            // Convert the SoftwareBitmap to a compatible format if necessary
            SoftwareBitmap bitmap;
            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                bitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }
            else
            {
                bitmap = softwareBitmap;
            }

            // Encode the image to a stream
            using (var stream = new InMemoryRandomAccessStream())
            {
                // Choose the encoder (e.g., PNG, JPEG)
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);

                // Set the SoftwareBitmap as the source
                encoder.SetSoftwareBitmap(bitmap);

                // Flush the encoder to write the data to the stream
                await encoder.FlushAsync();

                // Read the stream into a byte array
                stream.Seek(0);
                var bytes = new byte[stream.Size];
                await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);

                return bytes;
            }
        }

        private async void TesseractOcrExtract()
        {
            TesseractEngine engine = new TesseractEngine(System.IO.Path.GetFullPath(@".\tessdata"), (LanguageList.SelectedValue as ComboBoxItem).Content.ToString(), EngineMode.Default);

            using (var img = Pix.LoadFromMemory(await SoftwareBitmapToByteArrayAsync(bitmap)))
            {
                using (var page = engine.Process(img, PageSegMode.Auto))
                {
                    foreach (var word in page.GetWordStrBoxText(1).Split("\n"))
                    {
                        if (!word.StartsWith("WordStr"))
                        {
                            continue;
                        }

                        var split = word.Split("#", StringSplitOptions.RemoveEmptyEntries);
                        var coords = split[0].Replace("WordStr ", "").Split(" ", StringSplitOptions.RemoveEmptyEntries);
                        var text = split[1].Trim();

                        var x1 = double.Parse(coords[0]);
                        var y1 = bitmap.PixelHeight - double.Parse(coords[3]);
                        var x2 = double.Parse(coords[2]);
                        var y2 = bitmap.PixelHeight - double.Parse(coords[1]);

                        var rect = new Rect(x1/2, y1/2, x2 - x1, y2 - y1);

                        var wordBoxOverlay = new WordOverlay(rect);

                        // Keep references to word boxes.
                        wordBoxes.Add(wordBoxOverlay);

                        // Create a box to highlight the word.
                        TextOverlay.Children.Add(wordBoxOverlay.CreateBorder(HighlightedWordBoxHorizontalLineStyle));
                    }

                    // Rescale word boxes to match current UI size.
                    UpdateWordBoxTransform();

                    ExtractedTextBox.Text = page.GetText();
                }
            }
        }

        private async void MicrosoftOcrExtract()
        {
            // Check if OcrEngine supports image resolution.
            if (bitmap.PixelWidth > OcrEngine.MaxImageDimension || bitmap.PixelHeight > OcrEngine.MaxImageDimension)
            {
                rootPage.NotifyUser(
                    String.Format("Bitmap dimensions ({0}x{1}) are too big for OCR.", bitmap.PixelWidth, bitmap.PixelHeight) +
                    "Max image dimension is " + OcrEngine.MaxImageDimension + ".",
                    NotifyType.ErrorMessage);

                return;
            }

            OcrEngine ocrEngine = null;

            if (UserLanguageToggle.IsOn)
            {
                // Try to create OcrEngine for first supported language from UserProfile.GlobalizationPreferences.Languages list.
                // If none of the languages are available on device, method returns null.
                ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            }
            else
            {
                // Try to create OcrEngine for specified language.
                // If language is not supported on device, method returns null.
                ocrEngine = OcrEngine.TryCreateFromLanguage(LanguageList.SelectedValue as Language);
            }

            if (ocrEngine != null)
            {
                // Recognize text from image.
                var ocrResult = await ocrEngine.RecognizeAsync(bitmap);

                // Display recognized text.
                ExtractedTextBox.Text = ocrResult.Text;

                if (ocrResult.TextAngle != null)
                {
                    // If text is detected under some angle in this sample scenario we want to
                    // overlay word boxes over original image, so we rotate overlay boxes.
                    TextOverlay.RenderTransform = new RotateTransform
                    {
                        Angle = (double)ocrResult.TextAngle,
                        CenterX = PreviewImage.ActualWidth / 2,
                        CenterY = PreviewImage.ActualHeight / 2
                    };
                }

                // Create overlay boxes over recognized words.
                foreach (var line in ocrResult.Lines)
                {
                    // Determine if line is horizontal or vertical.
                    // Vertical lines are supported only in Chinese Traditional and Japanese languages.
                    Rect lineRect = Rect.Empty;
                    foreach (var word in line.Words)
                    {
                        lineRect.Union(word.BoundingRect);
                    }
                    bool isVerticalLine = lineRect.Height > lineRect.Width;
                    var style = isVerticalLine ? HighlightedWordBoxVerticalLineStyle : HighlightedWordBoxHorizontalLineStyle;

                    foreach (var word in line.Words)
                    {
                        WordOverlay wordBoxOverlay = new WordOverlay(word);

                        // Keep references to word boxes.
                        wordBoxes.Add(wordBoxOverlay);

                        // Create a box to highlight the word.
                        TextOverlay.Children.Add(wordBoxOverlay.CreateBorder(style));
                    }
                }

                // Rescale word boxes to match current UI size.
                UpdateWordBoxTransform();

                rootPage.NotifyUser(
                    "Image is OCRed for " + ocrEngine.RecognizerLanguage.DisplayName + " language.",
                    NotifyType.StatusMessage);
            }
            else
            {
                rootPage.NotifyUser("Selected language is not available.", NotifyType.ErrorMessage);
            }
        }

        /// <summary>
        /// This is event handler for 'Extract' button.
        /// Recognizes text from image and displays it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ExtractButton_Click(object sender, RoutedEventArgs e)
        {
            ClearResults();

            switch (OcrEngineList.SelectedIndex)
            {
                case 0: // Tesseract
                    TesseractOcrExtract();
                    break;

                case 1: // Microsoft
                    MicrosoftOcrExtract();
                    break;

                case 2: // Azure
                default:
                    break;
            }
        }

        /// <summary>
        /// Occurs when user language toggle state is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserLanguageToggle_Toggled(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            UpdateAvailableLanguages();
        }

        /// <summary>
        /// This is event handler for 'Sample' button.
        /// It loads sample image and displays it in UI.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SampleButton_Click(object sender, RoutedEventArgs e)
        {
            ClearResults();

            await LoadSampleImageAsync();

            rootPage.NotifyUser("Loaded sample image.", NotifyType.StatusMessage);
        }

        /// <summary>
        /// This is event handler for 'Load' button.
        /// It opens file picked and load selected image in UI..
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker()
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                FileTypeFilter = { ".jpg", ".jpeg", ".png" },
            };

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                ClearResults();

                await LoadImageAsync(file);

                rootPage.NotifyUser(
                    String.Format("Loaded image from file: {0} ({1}x{2}).", file.Name, bitmap.PixelWidth, bitmap.PixelHeight),
                    NotifyType.StatusMessage);
            }
        }

        /// <summary>
        /// Occurs when selected language is changed in available languages combo box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OcrEngineList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClearResults();
            UpdateAvailableLanguages();
        }

        /// <summary>
        /// Occurs when selected language is changed in available languages combo box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LanguageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClearResults();
        }

        /// <summary>
        /// Occurs when displayed image size changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PreviewImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Update word overlay boxes.
            UpdateWordBoxTransform();

            // Update image rotation center.
            var rotate = TextOverlay.RenderTransform as RotateTransform;
            if (rotate != null)
            {
                rotate.CenterX = PreviewImage.ActualWidth / 2;
                rotate.CenterY = PreviewImage.ActualHeight / 2;
            }
        }
    }
}
