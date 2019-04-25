using System;
using System.Diagnostics;
using System.IO;
using Windows.Storage;

namespace FullTrustLauncher
{
    class Generator
    {
        private const string FILE_TEST_FOLDER = @"C:\Users\phste\Downloads\SearchTest\";
        private const int MAX_FILES = 2000;
        private static int curFiles = 0;

        static void Main(string[] args)
        {
            CreateFiles("", -1);
        }

        private static void CreateFiles(string fileName, int pos)
        {
            if (curFiles >= MAX_FILES) return;

            for (int j = 0; j < 26; j++)
            {
                var name = fileName + (char)('a' + j) + ".txt";
                File.Create(FILE_TEST_FOLDER + name).Dispose();
                Console.WriteLine("Created " + name);
                curFiles++;
            }

            if (pos == -1)
            {
                CreateFiles(fileName + "a", pos + 1);
                return;
            }

            var lastChar = fileName[pos];
            if (lastChar != 'z') CreateFiles(fileName.Substring(0, fileName.Length - 1) + (char)(lastChar + 1), pos);
            else CreateFiles(fileName + "a", pos + 1);
        }
    }
}
