using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace XDS.Messaging.SDK.AppSupport.NetStandard.ActiveItemRegex
{
    public class ActiveItemRegex
    {
        string hashtagPattern = @"(?:^|\s|$)#[\p{L}0-9_]*";
        string mentionPattern = @"(?:^|\s|$|[.])@[\p{L}0-9_]*";
        string urlPattern = @"((http|ftp|https):\/\/)*[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?";

        private List<ActiveItem> RegexDeclaration(string text, string pattern, RegexType type)
        {
            var listOfItems = new List<ActiveItem>();

            if (!string.IsNullOrEmpty(text))
            {
                var regex = new Regex(pattern);

                var items = regex.Matches(text).Cast<Match>().ToList(); ;

                foreach (var item in items)
                {
                    var startIndex = item.Index;
                    var lastIndex = item.Index + item.Length;

                    listOfItems.Add(new ActiveItem(item.Value, type, startIndex, lastIndex));
                }
            }

            return listOfItems;
        }

        public List<ActiveItem> GetAllElements(string text)
        {
            var activeItems = new List<ActiveItem>();

            activeItems.AddRange(RegexDeclaration(text, this.hashtagPattern, RegexType.Hashtag));
            activeItems.AddRange(RegexDeclaration(text, this.mentionPattern, RegexType.Username));
            activeItems.AddRange(RegexDeclaration(text, this.urlPattern, RegexType.Url));

            return activeItems;
        }
    }
}
