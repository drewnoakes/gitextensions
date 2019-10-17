﻿using System;
using System.Collections.Generic;

namespace GitUIPluginInterfaces
{
    public readonly struct Remote
    {
        public readonly string Name { get; }
        public readonly string FetchUrl { get; }
        public readonly List<string> PushUrls { get; }

        public Remote(string name, string fetchUrl, string firstPushUrl)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            FetchUrl = fetchUrl ?? throw new ArgumentNullException(nameof(fetchUrl));

            // At least one push URL must be added
            PushUrls = new List<string>() { firstPushUrl ?? throw new ArgumentNullException(nameof(firstPushUrl)) };
        }
    }
}