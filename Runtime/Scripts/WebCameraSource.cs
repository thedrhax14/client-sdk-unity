using UnityEngine;
using LiveKit.Proto;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;
using System.Runtime.InteropServices;

namespace LiveKit
{
    // VideoSource for Unity WebCamTexture
    public class WebCameraSource : RtcVideoSource
    {
        TextureFormat _textureFormat;
        private RenderTexture _tempTexture;

        public WebCamTexture Texture { get; }

        public override int GetWidth()
        {
            return Texture.width;
        }

        public override int GetHeight()
        {
            return Texture.height;
        }

        protected override VideoRotation GetVideoRotation()
        {
            switch (Texture.videoRotationAngle)
            {
                case 90: return VideoRotation._90;
                case 180: return VideoRotation._0;
            }
            return VideoRotation._180;
        }

        public WebCameraSource(WebCamTexture texture, VideoBufferType bufferType = VideoBufferType.Rgba) : base(VideoStreamSource.Texture, bufferType)
        {
            Texture = texture;
            base.Init();
        }

        ~WebCameraSource()
        {
            Dispose(false);
        }

        private Color32[] _readBuffer;

        // Read the texture data into a native array asynchronously
        protected override bool ReadBuffer()
        {
            if (_reading && !Texture.isPlaying)
                return false;
            _reading = true;
            var textureChanged = false;

            int width = GetWidth();
            int height = GetHeight();

            if (_previewTexture == null ||
                _previewTexture.width != width ||
                _previewTexture.height != height)
            {
                Debug.Log("Creating new texture");
                // Required when using Allocator.Persistent
                if (_captureBuffer.IsCreated)
                    _captureBuffer.Dispose();

                var compatibleFormat = SystemInfo.GetCompatibleFormat(Texture.graphicsFormat, FormatUsage.ReadPixels);
                _textureFormat = GraphicsFormatUtility.GetTextureFormat(compatibleFormat);
                _bufferType = GetVideoBufferType(_textureFormat);

                _readBuffer = new Color32[width * height];
                _previewTexture = new Texture2D(width, height, TextureFormat.BGRA32, false);
                _captureBuffer = new NativeArray<byte>(width * height * GetStrideForBuffer(_bufferType), Allocator.Persistent);

                if (Texture.graphicsFormat != _previewTexture.graphicsFormat)
                    _tempTexture = new RenderTexture(width, height, 0, _previewTexture.graphicsFormat);

                textureChanged = true;
            }

            Texture.GetPixels32(_readBuffer);
            MemoryMarshal.Cast<Color32, byte>(_readBuffer)
                .CopyTo(_captureBuffer.AsSpan());

            _requestPending = true;

            if (Texture.graphicsFormat != _previewTexture.graphicsFormat)
            {
                Graphics.Blit(Texture, _tempTexture);
                Graphics.CopyTexture(_tempTexture, _previewTexture);
            }
            else
            {
#if UNITY_6000_0_OR_NEWER && UNITY_IOS
                _previewTexture.SetPixels(CamTexture.GetPixels());
                _previewTexture.Apply();
#else
                Graphics.CopyTexture(Texture, _previewTexture);
#endif
            }

            return textureChanged;
        }
    }
}

