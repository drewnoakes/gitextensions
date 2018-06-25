﻿using System;
using System.Drawing;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Gravatar
{
    /// <summary>
    /// Caches most-recently-used images.
    /// </summary>
    /// <remarks>
    /// Decorates an inner cache, delegating to it as needed.
    /// <para />
    /// If an image is available in memory, the inner cache can be bypassed.
    /// </remarks>
    public sealed class MruImageCache : IImageCache
    {
        private readonly MruCache<string, Image> _cache;
        private readonly IImageCache _inner;

        public MruImageCache([NotNull] IImageCache inner, int capacity = 30)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _cache = new MruCache<string, Image>(capacity);
        }

        /// <inheritdoc />
        event EventHandler IImageCache.Invalidated
        {
            add => _inner.Invalidated += value;
            remove => _inner.Invalidated -= value;
        }

        /// <inheritdoc />
        void IImageCache.AddImage(string imageFileName, Image image)
        {
            if (string.IsNullOrWhiteSpace(imageFileName))
            {
                throw new ArgumentException(nameof(imageFileName));
            }

            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            _inner.AddImage(imageFileName, image);

            lock (_cache)
            {
                _cache.Add(imageFileName, image);
            }
        }

        /// <inheritdoc />
        Task IImageCache.ClearAsync()
        {
            lock (_cache)
            {
                _cache.Clear();
            }

            return _inner.ClearAsync();
        }

        /// <inheritdoc />
        Task IImageCache.DeleteImageAsync(string imageFileName)
        {
            if (string.IsNullOrWhiteSpace(imageFileName))
            {
                throw new ArgumentException(nameof(imageFileName));
            }

            lock (_cache)
            {
                _cache.TryRemove(imageFileName, out _);
            }

            return _inner.DeleteImageAsync(imageFileName);
        }

        /// <inheritdoc />
        Image IImageCache.GetImage(string imageFileName)
        {
            if (string.IsNullOrWhiteSpace(imageFileName))
            {
                throw new ArgumentException(nameof(imageFileName));
            }

            lock (_cache)
            {
                if (_cache.TryGetValue(imageFileName, out var cachedImage))
                {
                    return cachedImage;
                }
            }

            var image = _inner.GetImage(imageFileName);

            lock (_cache)
            {
                if (image != null)
                {
                    _cache.Add(imageFileName, image);
                }

                return image;
            }
        }

        /// <inheritdoc />
        async Task<Image> IImageCache.GetImageAsync(string imageFileName)
        {
            if (string.IsNullOrWhiteSpace(imageFileName))
            {
                throw new ArgumentException(nameof(imageFileName));
            }

            lock (_cache)
            {
                if (_cache.TryGetValue(imageFileName, out var cachedImage))
                {
                    return cachedImage;
                }
            }

            var image = await _inner.GetImageAsync(imageFileName);

            lock (_cache)
            {
                if (image != null)
                {
                    _cache.Add(imageFileName, image);
                }

                return image;
            }
        }
    }
}