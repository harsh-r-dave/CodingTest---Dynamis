using Microsoft.AspNetCore.Http;
using System;
using System.IO;

namespace WebApplication4.HelperClasses
{
    public class Utility
    {
        // METHOD TO CHECK USER'S AGE, 
        // RETURNS TOTAL NUMBER OF DAYS BETWEEN TODAY AND USER'S BIRTHDAY 
        public static double AgeSpan(DateTime birthDate)
        {
            return DateTime.Now.Subtract(birthDate).TotalDays;
        }

        // RENAME FILENAME BEFORE SAVING IT TO DATABASE
        public static string GetProfilePictureFileName(IFormFile file)
        {
            var fileName = Path.GetFileNameWithoutExtension(file.FileName);
            var extension = Path.GetExtension(file.FileName);
            fileName = fileName + DateTime.Now.ToString("yymmssfff") + extension;
            return fileName;
        }
    }
}
