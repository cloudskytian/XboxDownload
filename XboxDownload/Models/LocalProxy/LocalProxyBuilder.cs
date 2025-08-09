﻿using System.Collections.ObjectModel;

namespace XboxDownload.Models.LocalProxy;

public static class LocalProxyBuilder
{
    public static ObservableCollection<ProxyModels> GetProxyRulesList() =>
    [
        new ("Steam", ["store.steampowered.com", "api.steampowered.com", "login.steampowered.com", "help.steampowered.com", "checkout.steampowered.com", "steamcommunity.com"]),
        new ("GitHub", ["*github.blog", "*github.com", "*github.io", "*githubassets.com", "*githubstatus.com", "*githubusercontent.com"] ),
        new ("Wikipedia", ["*wikipedia.org, *wikimedia.org, *mediawiki.org, *wikibooks.org, *wikidata.org, *wikinews.org, *wikiquote.org, *wikisource.org, *wikiversity.org, *wikivoyage.org, *wiktionary.org, *wikimediafoundation.org, *wmfusercontent.org, *wikifunctions.org, w.wiki | 2a02:ec80:600:ed1a::1, 2a02:ec80:300:ed1a::1, 2620:0:860:ed1a::1, 2620:0:861:ed1a::1, 2a02:ec80:700:ed1a::1, 185.15.59.224, 185.15.58.224, 208.80.153.224, 195.200.68.224", "upload.wikimedia.org | 2620:0:861:ed1a::2:b, 2620:0:860:ed1a::2:b, 2620:0:863:ed1a::2:b, 2a02:ec80:300:ed1a::2:b, 2001:df2:e500:ed1a::2:b, 2a02:ec80:600:ed1a::2:b, 2a02:ec80:700:ed1a::2:b, 185.15.59.240, 185.15.58.240, 208.80.153.240, 208.80.154.240, 195.200.68.240"] ),
        new ("Pixiv", [ "*pixiv.net -> pixiv.net", "*.pximg.net -> pximg.net"]),
        //new ("XXX", [ "www.xvideos.com", "*t66y.com"])
    ];
}