class Program
{
    static void Main(string[] args)
    {
        string sourceFolder = @"C:\監控資料夾";
        string destinationFolder = @"C:\目標資料夾";
        string logFilePath = @"C:\logs\file_movement.log";

        using (var monitor = new FolderMonitor(sourceFolder, destinationFolder, logFilePath))
        {
            Console.WriteLine("開始監控資料夾...");
            Console.WriteLine("按任意鍵結束");
            Console.ReadKey();
        }
    }
} 