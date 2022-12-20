using System;
using System.Data;
using System.IO;
using System.Text;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.Data.SQLite;
using Dapper;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VS_Local_Packages_Cleaner.CatalogJson;

namespace VS_Local_Packages_Cleaner
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
            bool readCatalogJson = Convert.ToBoolean(config.GetSection("AppSettings")["readCatalogJson"]);
            string connString = config.GetConnectionString("sqlite");
            bool resetVerNum = Convert.ToBoolean(config.GetSection("AppSettings")["resetVerNum"]);
            string strDropTableSql = config.GetSection("AppSettings")["sqliteDropTable"];
            string strCreateTableSql = config.GetSection("AppSettings")["sqliteCreateTable"];
            string strUpdateSql = config.GetSection("AppSettings")["sqliteUpdate1"] +
                config.GetSection("AppSettings")["sqliteUpdate2"] +
                config.GetSection("AppSettings")["sqliteUpdate3"] +
                config.GetSection("AppSettings")["sqliteUpdate4"] +
                config.GetSection("AppSettings")["sqliteUpdate5"] +
                config.GetSection("AppSettings")["sqliteUpdate6"];
            string strUpdateSql223 = config.GetSection("AppSettings")["sqliteUpdate1"];
            string strQuerySql = config.GetSection("AppSettings")["sqliteQuery"];
            DateTime newfileTimeline = Convert.ToDateTime(config.GetSection("AppSettings")["newfileTimeline"]);

            Console.WriteLine("Local Visual Studio installation package full path: ");
            var dirPackages = new DirectoryInfo(Console.ReadLine().Trim());
            if (!dirPackages.Exists)
            {
                Console.WriteLine("Directory does not exist !");
            }
            else
            {
                var _fileName = dirPackages.Name + DateTime.Now.ToString("yyyyMMddHHmmss");
                connString = connString.Replace("[PackagesFileName]", _fileName);

                var dtPackages = new DataTable("Packages");
                dtPackages.Columns.Add(new DataColumn("DirectoryName", typeof(string)));
                dtPackages.Columns.Add(new DataColumn("PackageName", typeof(string)));
                dtPackages.Columns.Add(new DataColumn("PackageNameNoVer", typeof(string)));
                dtPackages.Columns.Add(new DataColumn("LastWriteTime", typeof(DateTime)));
                dtPackages.Columns.Add(new DataColumn("ToDelete", typeof(bool)));

                // 1) Get all package list for operation
                foreach (var pkg in dirPackages.GetDirectories())
                {
                    if (pkg.Name == "certificates" || pkg.Name == "Archive")
                        continue;
                    //if (!pkg.Name.Contains("Microsoft.AspNetCore.SharedFramework.3"))
                    //    continue; // for debug only, comment the line before release

                    var pkgName = (pkg.Name.IndexOf(",") > 0) ? (pkg.Name.Substring(0, pkg.Name.IndexOf(","))) : (pkg.Name);
                    if (pkg.Name.ToLower().Contains("x86") && !pkgName.ToLower().Contains("x86")) pkgName += ".x86";
                    if (pkg.Name.ToLower().Contains("x64") && !pkgName.ToLower().Contains("x64")) pkgName += ".x64";
                    if (pkg.Name.ToLower().Contains("arm64") && !pkgName.ToLower().Contains("arm64")) pkgName += ".arm64";
                    if (pkg.Name.ToLower().Contains("en-us") && !pkgName.ToLower().Contains("en-us")) pkgName += ".en-us";
                    if (pkg.Name.ToLower().Contains("zh-cn") && !pkgName.ToLower().Contains("zh-cn")) pkgName += ".zh-cn";

                    var majorVer = Convert.ToInt32(config.GetSection("AppSettings")["majorVer"]);
                    if (pkg.Name.Contains("Microsoft.iOS")) majorVer = 1;
                    if (pkg.Name.Contains("Microsoft.Net.")) majorVer = 3;
                    if (pkg.Name.Contains("Microsoft.VC.")) majorVer = 4;
                    if (pkg.Name.Contains("Win10_")) majorVer = 3;
                    if (pkg.Name.Contains("Win10SDK_")) majorVer = 3;
                    if (pkg.Name.Contains("Win11SDK_")) majorVer = 3;

                    var dr = dtPackages.NewRow();
                    dr["DirectoryName"] = pkg.Name; // 0
                    dr["PackageName"] = pkgName; // 1
                    dr["PackageNameNoVer"] = GetNoMinorVersion(pkgName, majorVer, resetVerNum); // 2
                    dr["LastWriteTime"] = pkg.LastWriteTimeUtc; // 3
                    dr["ToDelete"] = false; // 4
                    dtPackages.Rows.Add(dr);

                    Console.WriteLine(dtPackages.Rows.Count + "\t" + dr[0] + "\t" + dr[3]);
                }

                // 2.1) Save package table to sqlite;
                var toDeleteDirectoryNames = DealDataWithSqlite(dtPackages, newfileTimeline, connString, strDropTableSql, strCreateTableSql, strUpdateSql, strQuerySql).Result;

                // 2.2) Compare directories in Catalog.Json with the directories in installation path
                if (readCatalogJson)
                {
                    // 2.2.1) Find directories in Catalog.json
                    var allDirectories = new Dictionary<string, VSPackage>();
                    var fileCatalogJson = dirPackages + "\\Catalog.json";
                    if (File.Exists(fileCatalogJson))
                    {
                        var objCatalogJson = JObject.Parse(File.ReadAllText(fileCatalogJson, Encoding.UTF8));
                        foreach (var p in JsonConvert.DeserializeObject<List<VSPackage>>(objCatalogJson["packages"].ToString()))
                            if (!allDirectories.ContainsKey(p.ToString()))
                                allDirectories.Add(p.ToString().ToLower(), p);

                        // 2.2.2) Mark to delete directories if not exist in Catalog.json
                        var notContainDirectories = new List<string>();
                        var strDelDirForSqlite = new StringBuilder();
                        foreach (DataRow dr in dtPackages.Rows)
                            if (!allDirectories.ContainsKey(dr["DirectoryName"].ToString().ToLower()))
                            {
                                notContainDirectories.Add(dr["DirectoryName"].ToString());
                                if (!toDeleteDirectoryNames.Contains(dr["DirectoryName"].ToString()))
                                {
                                    toDeleteDirectoryNames.Add(dr["DirectoryName"].ToString());
                                    if (strDelDirForSqlite.Length > 0)
                                        strDelDirForSqlite.Append(",\"" + dr["DirectoryName"].ToString() + "\"");
                                    else
                                        strDelDirForSqlite.Append("\"" + dr["DirectoryName"].ToString() + "\"");
                                }
                            }

                        // 2.2.3) Update sqlite with mark "ToDelete"
                        if (strDelDirForSqlite.Length > 0)
                            UpdateDataWithSqlite(connString, strUpdateSql223 + "(" + strDelDirForSqlite.ToString() + ");");
                    }
                    else
                    {
                        Console.WriteLine("[Bypassing] Could not find Catalog.json file in " + dirPackages);
                    }
                }

                // 3) Create clean script for directory marked as "ToDelete"
                CreateCleanerScript(Directory.GetCurrentDirectory(), _fileName + ".cmd", dirPackages.FullName, toDeleteDirectoryNames);
                // OR 3) Move directory marked as "ToDelete" to recyclebin
                // new DirectoryInfo(dirPackages.Root + "$RECYCLE.BIN").GetDirectories()
                // pending for using first solution
            }
            Console.WriteLine("New File Timeline: " + newfileTimeline.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("Press any key to exit ...");
            Console.ReadKey();
        }

        /// <summary>
        /// Mark minor version number as 0
        /// </summary>
        /// <param name="originalName">Input original name</param>
        /// <param name="majorVer">Number of version digits treat as major version</param>
        /// <returns>Output name</returns>
        private static string GetNoMinorVersion(string originalName, int majorVer = 2, bool resetVerNum = false)
        {
            var strRtn = new StringBuilder();
            var ver = 0;
            var nameSplit = originalName.Replace("-", ".").Replace("_", ".").Split('.');
            for (int i = 0; i < nameSplit.Length; i++)
            {
                var toCheck = nameSplit[i];
                if (toCheck.StartsWith("v") || toCheck.StartsWith("V"))
                {
                    toCheck = toCheck.Substring(1, toCheck.Length - 1);
                }
                if (!toCheck.All(c => char.IsDigit(c)))
                {
                    strRtn.Append(nameSplit[i] + ".");
                    if (resetVerNum)
                        ver = 0;
                }
                else
                {
                    if (ver < majorVer)
                    {
                        if (ver == 0)
                        {
                            if (i > 0 && !nameSplit[i - 1].All(c => char.IsDigit(c)) && i + 1 < nameSplit.Length && nameSplit[i + 1].All(c => char.IsDigit(c)))
                                ver++;
                            strRtn.Append(nameSplit[i] + ".");
                        }
                        else
                        {
                            if (i > 0 && nameSplit[i - 1].All(c => char.IsDigit(c)))
                                ver++;
                            strRtn.Append(nameSplit[i] + ".");
                        }
                    }
                    else
                    {
                        strRtn.Append("0.");
                    }
                }
            }
            if (strRtn.ToString()[strRtn.ToString().Length - 1].Equals('.'))
                return strRtn.ToString().Substring(0, strRtn.ToString().Length - 1);
            else
                return strRtn.ToString();
        }

        private static async Task<List<string>> DealDataWithSqlite(DataTable dtSource, DateTime newfileTimeline,
             string connString, string strDropTableSql, string strCreateTableSql, string strUpdateSql, string strQuerySql)
        {
            using (var conn = new SQLiteConnection(connString))
            {
                try
                {
                    conn.Open();

                    // 2.1.1) Drop table if exists
                    conn.Execute(strDropTableSql);

                    // 2.1.2) Create an empty table
                    conn.Execute(strCreateTableSql);

                    // 2.1.3) Loop and insert, save all packages into sqlite;
                    foreach (DataRow r in dtSource.Rows)
                        conn.Execute("INSERT INTO Packages(DirectoryName, PackageName, PackageNameNoVer, LastWriteTime, ToDelete) VALUES('" +
                            r["DirectoryName"].ToString() + "', '" +
                            r["PackageName"].ToString() + "', '" +
                            r["PackageNameNoVer"].ToString() + "', '" +
                            Convert.ToDateTime(r["LastWriteTime"]).ToString("yyyy-MM-dd HH:mm:ss") + "', '" +
                            Convert.ToBoolean(r["ToDelete"]).ToString() + "');");

                    // 2.1.4) Query & update which package should be marked as "ToDelete";
                    await conn.ExecuteAsync(strUpdateSql.Replace("yyyy-MM-dd HH:mm:ss", newfileTimeline.ToString("yyyy-MM-dd HH:mm:ss")));

                    // 2.1.5) Query and sync data back, from sqlite file to DataTable
                    var toDeleteDirectoryNames = new Dictionary<string, bool>();
                    var toDeleteReader = conn.ExecuteReader(strQuerySql);
                    while (toDeleteReader.Read())
                        toDeleteDirectoryNames.Add(toDeleteReader["DirectoryName"].ToString(), true);
                    foreach (DataRow r in dtSource.Rows)
                        if (toDeleteDirectoryNames.ContainsKey(r["DirectoryName"].ToString()))
                            r["ToDelete"] = true;

                    return toDeleteDirectoryNames.Keys.AsList();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    return null;
                }
                finally
                {
                    if (conn != null)
                    {
                        conn.Close();
                        conn.Dispose();
                    }
                }
            }
        }

        private static int UpdateDataWithSqlite(string connString, string strUpdateSql)
        {
            using (var conn = new SQLiteConnection(connString))
            {
                try
                {
                    conn.Open();

                    return conn.Execute(strUpdateSql);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    return -1;
                }
                finally
                {
                    if (conn != null)
                    {
                        conn.Close();
                        conn.Dispose();
                    }
                }
            }
        }

        private static void CreateCleanerScript(string outputPath, string outputFileName, string packagesPath, List<string> toDeleteDirectoryNames)
        {
            var f = outputPath + "\\" + outputFileName;

            if (File.Exists(Path.GetFullPath(f)))
                File.Delete(Path.GetFullPath(f));

            using (var fs = new FileStream(f, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (var sw = new StreamWriter(fs))
                {
                    sw.BaseStream.Seek(0, SeekOrigin.End);
                    sw.WriteLine(packagesPath.Substring(0, 1) + ":\n");
                    sw.WriteLine("CD \"" + packagesPath + "\"\n");
                    sw.WriteLine("RD /S /Q \"Archive\""); // Always try to remove this directory if exists
                    foreach (var str in toDeleteDirectoryNames)
                        sw.WriteLine("RD /S /Q \"" + str + "\"");
                    sw.WriteLine("\n@PAUSE");
                    sw.Flush();
                }
            }
        }
    }
}
