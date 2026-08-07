namespace EasyWords.Features
{
    public class WordItem
    {
        public string Word { get; set; } = "";
        public bool IsFav { get; set; }
        public string IconFav => IsFav ? "★" : "☆";
    }
}
