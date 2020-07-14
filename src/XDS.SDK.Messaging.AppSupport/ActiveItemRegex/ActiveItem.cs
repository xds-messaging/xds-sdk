namespace XDS.Messaging.SDK.AppSupport.NetStandard.ActiveItemRegex
{
    public class ActiveItem
    {
        public RegexType Type { get; set; }
        public string Text { get; set; }
        public int StartIndex { get; set; }
        public int LastIndex { get; set; }

        public ActiveItem(string text, RegexType type, int startIndex, int lastIndex)
        {
            this.Text = text;
            this.Type = type;
            this.StartIndex = startIndex;
            this.LastIndex = lastIndex;
        }

        public void Select()
        {
            switch (this.Type)
            {
                case RegexType.Hashtag:
                    HashtagAction();
                    break;
                case RegexType.Username:
                    UserNameAction();
                    break;
                case RegexType.Url:
                    UrlAction();
                    break;
            }
        }

        void HashtagAction()
        {
            //Console.WriteLine("#");
            //TODO
        }

        void UrlAction()
        {
            //Console.WriteLine("https://...");
            //TODO
        }

        void UserNameAction()
        {
            //Console.WriteLine("@");
            //TODO
        }
    }
}
