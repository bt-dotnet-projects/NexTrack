//using System.Security.Cryptography;
//using System.Text;

//namespace ActivityTracker.Helpers;

//public static class MachineHelper
//{
//    private static string? _machineId;


//    public static string GetMachineId()
//    {
//        if (_machineId != null)
//            return _machineId;


//        string source =
//            $"{Environment.MachineName}-" +
//            $"{Environment.UserName}-" +
//            $"{Environment.OSVersion}";


//        using SHA256 sha = SHA256.Create();


//        byte[] bytes =
//            sha.ComputeHash(
//                Encoding.UTF8.GetBytes(source));


//        _machineId =
//            Convert.ToHexString(bytes);


//        return _machineId;
//    }


//    public static string GetUserName()
//    {
//        return Environment.UserName;
//    }


//    public static string GetComputerName()
//    {
//        return Environment.MachineName;
//    }
//}