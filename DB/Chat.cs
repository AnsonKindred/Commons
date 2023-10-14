namespace Commons
{
    public class Chat
    {
        public int ID { get; set; }
        public required int ServerID { get; set; }
        public required string Content { get; set; }
        public required int ClientID { get; set; }
        public required long Timestamp { get; set; }

        public virtual required Client Client { get; set; }
        public virtual required Server Server { get; set; }
    }
}
