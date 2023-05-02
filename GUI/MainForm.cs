using Dapper;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VS_Local_Packages_Cleaner.CatalogJson;

namespace VS_Local_Packages_Cleaner
{
    public partial class MainForm : Form
    {
        private string _vsDir;
        public string VSDir
        {
            set
            {
                _vsDir = value;
                if (_vsDir != string.Empty)
                    txtInfo.Text += "Local Visual Studio installation cache path selected:" + Environment.NewLine +
                        "    " + _vsDir + Environment.NewLine;
                btnCheck.Enabled = (_vsDir == string.Empty) ? (false) : (true);
            }
            get
            {
                return _vsDir;
            }
        }

        public MainForm()
        {
            InitializeComponent();
            _vsDir = string.Empty;
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;
        }
        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            VSDir = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
            return;
        }

        private void btnVSDir_Click(object sender, EventArgs e)
        {
            if (vsDirFolderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    VSDir = vsDirFolderBrowserDialog.SelectedPath;
                    if (VSDir != string.Empty)
                        btnCheck.Focus();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            VSDir = string.Empty;
            txtInfo.Text = string.Empty;
        }

        private void btnCheck_Click(object sender, EventArgs e)
        {
            #region Get config values
            var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
            string connString = config.GetConnectionString("sqlite");

            bool resetVerNum = Convert.ToBoolean(config.GetSection("AppSettings")["resetVerNum"]);
            string strDropTableSql = config.GetSection("AppSettings")["sqliteDropTable"];
            string strCreateTableSql = config.GetSection("AppSettings")["sqliteCreateTable"];
            string strUpdateSql = config.GetSection("AppSettings")["sqliteUpdate1"] +
                config.GetSection("AppSettings")["sqliteUpdate2"] +
                config.GetSection("AppSettings")["sqliteUpdate3"] +
                config.GetSection("AppSettings")["sqliteUpdate4"] +
                config.GetSection("AppSettings")["sqliteUpdate5"];
            DateTime newfileTimeline = Convert.ToDateTime(config.GetSection("AppSettings")["newfileTimeline"]);
            if (newfileTimeline < DateTime.Now.AddDays(-3))
                newfileTimeline = DateTime.Now.AddDays(-3);

            bool readCatalogJson = Convert.ToBoolean(config.GetSection("AppSettings")["readCatalogJson"]);
            string strDropTableAllSql = config.GetSection("AppSettings")["sqliteDropTableAll"];
            string strCreateTableAllSql = config.GetSection("AppSettings")["sqliteCreateTableAll"];
            string strUpdateAllSql = config.GetSection("AppSettings")["sqliteUpdate1"] +
                config.GetSection("AppSettings")["sqliteUpdateAll"];

            string strQuerySql = config.GetSection("AppSettings")["sqliteQuery"];
            #endregion

            var dirPackages = new DirectoryInfo(VSDir.Trim());
            if (!dirPackages.Exists)
            {
                txtInfo.Text += "Directory does not exist! Please select again!" + Environment.NewLine;
                VSDir = string.Empty;
                return;
            }
            else
            {
                var _result = dirPackages.Name + DateTime.Now.ToString("yyyyMMddHHmmss");
                connString = connString.Replace("[PackagesFileName]", _result);

                var dtPackages = new DataTable("Packages");
                dtPackages.Columns.Add(new DataColumn("DirectoryName", typeof(string))); // 0
                dtPackages.Columns.Add(new DataColumn("PackageName", typeof(string))); // 1
                dtPackages.Columns.Add(new DataColumn("PackageNameNoVer", typeof(string))); // 2
                dtPackages.Columns.Add(new DataColumn("LastWriteTime", typeof(DateTime))); // 3
                dtPackages.Columns.Add(new DataColumn("ToDelete", typeof(bool))); // 4

                // 1) Get all package list from directory
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
                    dr["DirectoryName"] = pkg.Name.ToLower(); // 0
                    dr["PackageName"] = pkgName; // 1
                    dr["PackageNameNoVer"] = GetNoMinorVersion(pkgName, majorVer, resetVerNum); // 2
                    dr["LastWriteTime"] = pkg.LastWriteTimeUtc; // 3
                    dr["ToDelete"] = false; // 4
                    dtPackages.Rows.Add(dr);

                    // DO NOT display all packages in text box since too slow
                    // txtInfo.Text += dtPackages.Rows.Count + " " + dr[0] + "\t" + dr[3] + Environment.NewLine;
                }
                txtInfo.Text += "Operation " + dtPackages.Rows.Count + " packages..." + Environment.NewLine;

                // 2.1) Save package table to sqlite;
                DealDataInSqlite(dtPackages, newfileTimeline, connString, strDropTableSql, strCreateTableSql, strUpdateSql);

                // 2.2) Compare directories in Catalog.Json with the directories in installation path
                if (readCatalogJson)
                {
                    // 2.2.1) Fetch directories in Catalog.json
                    var allDirectories = new Dictionary<string, VSPackage>();
                    var fileCatalogJson = dirPackages + "\\Catalog.json";
                    if (File.Exists(fileCatalogJson))
                    {
                        try
                        {
                            var objCatalogJson = JObject.Parse(File.ReadAllText(fileCatalogJson, Encoding.UTF8));
                            foreach (var p in JsonConvert.DeserializeObject<List<VSPackage>>(objCatalogJson["packages"].ToString()))
                                if (!allDirectories.ContainsKey(p.ToString()))
                                    allDirectories.Add(p.ToString().ToLower(), p); // Try using ToString() to joint full directory name from catalog.json -> packages.

                            // 2.2.2) Mark to delete directories if not exist in Catalog.json
                            DealDateWithCatelogInSqlite(allDirectories.Keys.ToList<string>(), connString, strDropTableAllSql, strCreateTableAllSql, strUpdateAllSql);
                        }
                        catch (Exception ex)
                        {
                            txtInfo.Text += ex.ToString() + Environment.NewLine;
                        }
                    }
                    else
                    {
                        txtInfo.Text += "[Bypassing] Could not find Catalog.json file in " + dirPackages + Environment.NewLine;
                    }
                }

                // 2.3) Query directories to delete
                var toDeleteDirectories = QueryDateInSqlite(connString, strQuerySql);
                txtInfo.Text += " " + toDeleteDirectories.Count + " packages marked to delete." + Environment.NewLine;

                // 3) Create clean script for directory marked as "ToDelete"
                CreateCleanerScript(Directory.GetCurrentDirectory(), _result + ".cmd", dirPackages.FullName, toDeleteDirectories);
                txtInfo.Text += "Try running cleaning script standalone: " + Environment.NewLine +
                    "    " + Directory.GetCurrentDirectory() + "\\" + _result + ".cmd " + Environment.NewLine;
                // OR 3) Move directory marked as "ToDelete" to recyclebin
                // new DirectoryInfo(dirPackages.Root + "$RECYCLE.BIN").GetDirectories()
                // pending for using first solution
            }
            txtInfo.Text += "New File Timeline: " + newfileTimeline.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine;
            VSDir = string.Empty;
            txtInfo.Focus();
            txtInfo.Select(txtInfo.Text.Length, 0);
            txtInfo.ScrollToCaret();
        }

        /// <summary>
        /// Mark minor version number as 0
        /// </summary>
        /// <param name="originalName">Input original name</param>
        /// <param name="majorVer">Number of version digits treat as major version</param>
        /// <returns>Output name</returns>
        private string GetNoMinorVersion(string originalName, int majorVer = 2, bool resetVerNum = false)
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

        private bool DealDataInSqlite(DataTable dtSource, DateTime newfileTimeline,
            string connString, string strDropTableSql, string strCreateTableSql, string strUpdateSql)
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
                    using (var tran = conn.BeginTransaction())
                    {
                        foreach (DataRow r in dtSource.Rows)
                            conn.Execute("INSERT INTO [Packages] ([DirectoryName], [PackageName], [PackageNameNoVer], [LastWriteTime], [ToDelete]) VALUES('" +
                                r["DirectoryName"].ToString() + "', '" +
                                r["PackageName"].ToString() + "', '" +
                                r["PackageNameNoVer"].ToString() + "', '" +
                                Convert.ToDateTime(r["LastWriteTime"]).ToString("yyyy-MM-dd HH:mm:ss") + "', '" +
                                Convert.ToBoolean(r["ToDelete"]).ToString() + "');");
                        tran.Commit();
                    }

                    // 2.1.4) Update which package should be marked as "ToDelete";
                    conn.Execute(strUpdateSql.Replace("yyyy-MM-dd HH:mm:ss", newfileTimeline.ToString("yyyy-MM-dd HH:mm:ss")));

                    return true;
                }
                catch (Exception ex)
                {
                    txtInfo.Text += ex.ToString() + Environment.NewLine;
                    return false;
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

        private bool DealDateWithCatelogInSqlite(List<string> allDirectoriesInCatelog,
            string connString, string strDropTableSql, string strCreateTableSql, string strUpdateSql)
        {
            using (var conn = new SQLiteConnection(connString))
            {
                try
                {
                    conn.Open();

                    // 2.2.1) Drop table if exists
                    conn.Execute(strDropTableSql);

                    // 2.2.2) Create an empty table
                    conn.Execute(strCreateTableSql);

                    // 2.2.3) Loop and insert, save all directories into sqlite
                    using (var tran = conn.BeginTransaction())
                    {
                        foreach (var d in allDirectoriesInCatelog)
                            conn.Execute("INSERT INTO [All] ([DirectoryName]) VALUES('" + d + "');");
                        tran.Commit();
                    }

                    // 2.3.4) Update which package should be marked as "ToDelete";
                    conn.Execute(strUpdateSql);

                    return true;
                }
                catch (Exception ex)
                {
                    txtInfo.Text += ex.ToString() + Environment.NewLine;
                    return false;
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

        private List<string> QueryDateInSqlite(string connString, string strQuerySql)
        {
            using (var conn = new SQLiteConnection(connString))
            {
                try
                {
                    conn.Open();

                    var toDeleteDirectories = new List<string>();
                    var toDeleteReader = conn.ExecuteReader(strQuerySql);
                    while (toDeleteReader.Read())
                        toDeleteDirectories.Add(toDeleteReader["DirectoryName"].ToString());

                    return toDeleteDirectories;
                }
                catch (Exception ex)
                {
                    txtInfo.Text += ex.ToString() + Environment.NewLine;
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
