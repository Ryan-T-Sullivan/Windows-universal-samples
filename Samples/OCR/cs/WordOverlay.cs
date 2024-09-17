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

using System.ComponentModel;
using Windows.Foundation;
using Windows.Media.Ocr;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace SDKTemplate
{
    /// <summary>
    /// Wraps recognized word and provides word overlay position and size based on current scale.
    /// </summary>
    class WordOverlay : INotifyPropertyChanged
    {
        private Rect wordBoundingRect;
        private Rect scaledWordBoundingRect;

        /// <summary>
        /// Left and Right properties of Thickess define word box position.
        /// </summary>
        public Thickness WordPosition => new Thickness(wordBoundingRect.Left, wordBoundingRect.Top, 0, 0);

        /// <summary>
        /// Scaled word box width.
        /// </summary>
        public double WordWidth => scaledWordBoundingRect.Width;

        /// <summary>
        /// Scaled word box height.
        /// </summary>
        public double WordHeight => scaledWordBoundingRect.Height;

        public event PropertyChangedEventHandler PropertyChanged;

        public WordOverlay(Rect rect)
        {
            wordBoundingRect = rect;
        }

        public WordOverlay(OcrWord ocrWord)
        {
            wordBoundingRect = ocrWord.BoundingRect;

            UpdateProps(wordBoundingRect);
        }

        public void Transform(ScaleTransform scale)
        {
            // Scale word box bounding rect and update properties.
            UpdateProps(scale.TransformBounds(wordBoundingRect));
        }

        public Border CreateBorder(Style style, UIElement child = null)
        {
            var overlay = new Border()
            {
                Child = child,
                Style = style
            };

            // Bind word boxes to UI.
            overlay.SetBinding(FrameworkElement.MarginProperty, CreateBinding("WordPosition"));
            overlay.SetBinding(FrameworkElement.WidthProperty, CreateBinding("WordWidth"));
            overlay.SetBinding(FrameworkElement.HeightProperty, CreateBinding("WordHeight"));

            return overlay;
        }

        Binding CreateBinding(string propertyName) => new Binding() { Path = new PropertyPath(propertyName), Source = this };

        private void UpdateProps(Rect wordBoundingBox)
        {
            scaledWordBoundingRect = wordBoundingBox;

            OnPropertyChanged("WordPosition");
            OnPropertyChanged("WordWidth");
            OnPropertyChanged("WordHeight");
        }

        protected void OnPropertyChanged(string PropertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
