using System.Text;

namespace WolfConquer.Tests;

// Synthetic zero-key bytes from the independent public-domain reference; no game data.
internal static class Chinese7937EchoRuntimeFixture
{
    internal static byte[] CompleteDecoded() => Encoding.ASCII.GetBytes("local XuanBaoEchoInfo = {\n[\"max_hole_num\"] = 5,\n[\"cost_num\"] = 17,\n[\"cost_money_1\"] = 100,\n[\"cost_itemtype_1\"] = 42,\n[\"cost_itemnum_1\"] = 2,\n[\"cost_money_2\"] = 200,\n[\"cost_itemtype_2\"] = 42,\n[\"cost_itemnum_2\"] = 3,\n[\"open_hole2_cost_num\"] = 20,\n[\"open_hole2_itemtype_1\"] = 43,\n[\"open_hole2_exp_1\"] = 22,\n[\"open_hole2_itemtype_2\"] = 44,\n[\"open_hole2_exp_2\"] = 24,\n[\"open_hole2_maxexp\"] = 26,\n[\"cost_money_3\"] = 300,\n[\"cost_itemtype_3\"] = 42,\n[\"cost_itemnum_3\"] = 4,\n[\"open_hole3_cost_num\"] = 30,\n[\"open_hole3_itemtype_1\"] = 43,\n[\"open_hole3_exp_1\"] = 33,\n[\"open_hole3_itemtype_2\"] = 44,\n[\"open_hole3_exp_2\"] = 36,\n[\"open_hole3_maxexp\"] = 39,\n[\"cost_money_4\"] = 400,\n[\"cost_itemtype_4\"] = 42,\n[\"cost_itemnum_4\"] = 5,\n[\"open_hole4_cost_num\"] = 40,\n[\"open_hole4_itemtype_1\"] = 43,\n[\"open_hole4_exp_1\"] = 44,\n[\"open_hole4_itemtype_2\"] = 44,\n[\"open_hole4_exp_2\"] = 48,\n[\"open_hole4_maxexp\"] = 52,\n[\"cost_money_5\"] = 500,\n[\"cost_itemtype_5\"] = 42,\n[\"cost_itemnum_5\"] = 6,\n[\"open_hole5_cost_num\"] = 50,\n[\"open_hole5_itemtype_1\"] = 43,\n[\"open_hole5_exp_1\"] = 55,\n[\"open_hole5_itemtype_2\"] = 44,\n[\"open_hole5_exp_2\"] = 60,\n[\"open_hole5_maxexp\"] = 65,\n[\"cost_money_6\"] = 600,\n[\"cost_itemtype_6\"] = 42,\n[\"cost_itemnum_6\"] = 7,\n[\"open_hole6_cost_num\"] = 60,\n[\"open_hole6_itemtype_1\"] = 43,\n[\"open_hole6_exp_1\"] = 66,\n[\"open_hole6_itemtype_2\"] = 44,\n[\"open_hole6_exp_2\"] = 72,\n[\"open_hole6_maxexp\"] = 78,\n[\"cost_money_7\"] = 700,\n[\"cost_itemtype_7\"] = 42,\n[\"cost_itemnum_7\"] = 8,\n[\"open_hole7_cost_num\"] = 70,\n[\"open_hole7_itemtype_1\"] = 43,\n[\"open_hole7_exp_1\"] = 77,\n[\"open_hole7_itemtype_2\"] = 44,\n[\"open_hole7_exp_2\"] = 84,\n[\"open_hole7_maxexp\"] = 91,\n}\nlocal function GetXuanBaoEchoInfo(key)\nreturn XuanBaoEchoInfo[key]\nend\nlocal function Unload()\nXuanBaoEchoInfo = nil\nend\nlocal XuanBao = {\nGetXuanBaoEchoInfo = GetXuanBaoEchoInfo,\nUnload = Unload,\n}\nXuanBao_GetXuanBaoEchoInfo = XuanBao.GetXuanBaoEchoInfo\n");
    internal static byte[] CompleteEncoded() => Convert.FromBase64String(
        "6dHsz1WbnD2TcjIvWCB+fHwaM2ZelbITt1BtXWwXoHj3t1IssIIH94LWyDv+NbiXDBNPF4y03RZB7i2r/l2tu2S6DriujkWLgZ7v" +
        "uakfS1X3oXRUcEWY/5W5arPWXl/VyQHp9AO/y2TWtvFhEI+307skSVgf8T0QHnKKOB9Jw3HG0G+TmfRuBguDvMm4hx4ri50b0ijJ" +
        "bLzfYTneL9GR1mvpBmTLst/UXX44Q2BFysQObj9IER3nqzpKQh21c+/Uc1NjOhlWKM6dsY55boFL2NVPQUmfk4S1eoVQW5DQ3oLj" +
        "a0/3BCCB5ECkql39VxpZ3nkm+rW68VbfEsnY0zFGmf5biz3wkdYB9fgCjJt6rySN/yaSvwAS7HrSQk2X9jNQxmY2HVsKtvhsc/3B" +
        "KF7JWAJVt0ens7tRQd0MoEjElQD0JQWGqvTTyRRb8eVlqIcbCNrKQhzAlM//2NFOHgLlSdsNqp20BqjeKQ7YDTu6M/5zsDzPe8Zm" +
        "AHqRB4ilBGWFHgeHm+m91v+Z5OWwGIF8n8ftzL6uZpoi13ucPgrCh8+bgfeXwmPdpQZan1WXvHpMEI+307skSVgf8T0QHnKKOB9J" +
        "w3HG0G+TIscKJvw1esUme8x4JMWBNp1AVD7wiePLf77r2nLsFt1bIbf+l+psFjI/RBA7dTC7OBdG9IoTgitUJ0gJ+PuiUoF8HPZp" +
        "6s+7qNsPmXWcP3KMBaXdwZJvS2kgk6ewoVKeBhc+SdmZuJG5v1mbmG6Sedd861cRNFqlDJlPbpyfEdRgPhPzSXbI17AabTGUeqbt" +
        "1GSLPNBO0qtFENWFvwAS7HrSQk2X9jNQ/nBVxqA34v52PNeQXDq3qrTzwOtXYAb9iQfq03NFZK7oGDvYbopnvf+ae1gS+e2MSv/1" +
        "gbC0c4BEmpk4hIMA4OZl+m6+Ab7OksalnZF+69j+Ov+IbUyRSB+s1xqayDhD0RYjGwQmfQ6I2UXDHjZH7uk2EBYDMbI8vwAS7HrS" +
        "Qk2X9jNQuz0dNjWgpAxH+ttCvJTKDz2IpGiN3CP3fafoc/PVIMAgjOG4p2xSVHyGgLjLTdrNr7l+A+tggRlo1rK6rRtTS4Q4tqJp" +
        "FMGW0+aFztMRkJi3F1aNn4f/PL4rjVTwqYazycofSzaSIXZWjU+OvRQRudjPUUVhlP2Fzamn4AtX5Uu3GVgpg+CeV6hRZAOqRkIp" +
        "BDo9dI7YZDLr+sqpvLIGDf1ATXHReszN0kFWOIic9D1yo5fIOnSdN2f5eMGIbbKKyjTwlPcoHfNT0aynjVrfRVxmSs1NwLOHFyEI" +
        "WoxrsqWKzltOnacTGatlg0DTlrSXnrvcM0yEw4Thr4P+ojXC5eFtOqMMkiPYEWF8XEU+3vdFMdgXrenQAiCQoLfVEv7QUOvkAQCY" +
        "01hKa4AKpFkc0RlqQQjIcvyg83XNFNSQNuVMFq0FA7RKxuf9bjDK6ULyRJ6SNtlKwwHJlNGQcwSnjATJmApDNTKl/m3aobc7iiUo" +
        "K2iK6WxjmBcG5lCXb5dniUwjtPODXs8qzFjrBH64PpDIH4V9ql4V/r3aW1jArrpih5V4P6nk0CK2SrhnZ8zwhweX2iwVkpSAcHCm" +
        "HAqJICWgIXLUoWnANu2SNxGWuZ23Gia4pH+eOSMpS6LLZqyTcwSnjATJmApDNTKll2ORRvqDD2U40Jr3+d62zpi/U+xGGTHVjikX" +
        "zopj9qRY3htBFF+kve2YSvS1JqQ+P7RTOPmp0iqADJ710H2BVrpszA1OGh2mk2I8kExpQdnDoMp8K8AOJAbTSij+8AIS7pD71ADf" +
        "omLhzv5kDFCgyiWPc69vkBwFacnNxPmeteAwHIKFYJa1jhcM+ky6nDCUDI0+Qp+nCr9NlhBb7Z9F5+vrCKNgtDR9ZvcYrlsnj6c1" +
        "LeKeFf5Dyye9e0zVyFVK/r3aW1jArrpih5V4q39NuiKrsZyHhOZbvwAS7HrSQk2X9jNQ87wi1YoY3fwMD4OuwYYF/i4klDcvQMLZ" +
        "8Ens/61HpH0+tJvK/hBAgaPiLb6DoWqQhcCELayelcB/+RqwSIkt8hE42ZfMKj3W6t0qSu8CloBTF21+yXvGn1Mtnr2qAQJs4IAY" +
        "0+IQuXsIbRtaBhc+SdmZuJG5v1mbK6Y8Lb70tvqv/SWePTiKHek0rlRrz0I5Br+Yk/N0JS7ZNvo2vf2N03RJVK2vlG9iqxUfz3Gc" +
        "Qh6Gj3dzFtXEe/eWnjwZsZUwU47O6d1Z5Ned5DRBuCQcsSmlN9BjIhwUNXfQVg9QfOAI6uSJTlnYXK+GrfXSoTStz26F8xT/mfpF" +
        "/bXkIlTrtZKzZ6CQJAdyObtOVVft3K6YwbM0BZ1ApO+Y2UAJczVr6dHsz1WbnD2TcjIv3DQklArFE6YBJhQ5NXfQVg9QfOAI6uSJ" +
        "NVOutrsWdJK3mmUP+zB+AqUkRYlJ5KnCJGd5hxZ3zn2HnE+QPXZIqTs73NPClqPZRtmzqgywyXIu7zxosFf2ejFg2DSaOZPGRy81" +
        "CXJdaUnIujpzObtOVVft3K6YwbM0O14=");
    internal static byte[] IncompleteDecoded() => Encoding.ASCII.GetBytes("local XuanBaoEchoInfo = {\n[\"max_hole_num\"] = 5,\n}\nlocal function GetXuanBaoEchoInfo(key)\nreturn XuanBaoEchoInfo[key]\nend\nlocal function Unload()\nXuanBaoEchoInfo = nil\nend\nlocal XuanBao = {\nGetXuanBaoEchoInfo = GetXuanBaoEchoInfo,\nUnload = Unload,\n}\nXuanBao_GetXuanBaoEchoInfo = XuanBao.GetXuanBaoEchoInfo\n");
    internal static byte[] IncompleteEncoded() => Convert.FromBase64String(
        "6dHsz1WbnD2TcjIvWCB+fHwaM2ZelbITt1BtXWwXoHj3t1IssIIH94LWyDv+NbiXFRLtBoO9jF4OqDgsg2JBSQMi9aC0iIfqEjug" +
        "UdjLaBldXEly0lGjbrzTkZxXi47hcaJPde+1bsqAibN39j1s8av58KBRYnioN7Nc6SNiWEfSt9yTmXHEOe94Keyk41Nk3vOssZ5D" +
        "1C6G81eN8qpwd2YFUEh2pVQNE/A2pVNNRGn2rPUosvUoLApYNgYKpw3BcaJPde+1bsqAibN32j4IT6GUukCNBDkmNXfQVg9QfOAI" +
        "6uSJYEf3W1vVw7zzRtQiYO0RU48yc8o2s6aOTe/ozh1TpWIZZwwMzH82tU0rWUJqxH4HcotyQWsREEuNM7h4MwBlRSIQZ+ftl3sI" +
        "HToyO14=");
}
