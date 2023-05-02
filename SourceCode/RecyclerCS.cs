using Microsoft.VisualBasic.FileIO;
using Shell32; //Reference Microsoft Shell Controls And Automation on the COM tab.
using System.IO;

namespace VS_Local_Packages_Cleaner
{
    // How to access the recyclebin using C#
    // https://social.msdn.microsoft.com/Forums/vstudio/en-US/05e3628d-5d08-4cea-8821-d5302139cc0d/how-to-access-the-recyclebin-using-c?forum=csharpgeneral
    // https://stackoverflow.com/questions/6025311/how-to-restore-files-from-recycle-bin
    public class RecyclerCS
    {
        private Shell Shl;
        //private const long ssfBITBUCKET = 10;
        //private const int recycleNAME = 0;
        //private const int recyclePATH = 1;

        public void Delete(string Item)
        {
            FileSystem.DeleteFile(Item, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            //Gives the most control of dialogs.
        }

        public bool Restore(string Item)
        {
            Shl = new Shell();
            Folder Recycler = Shl.NameSpace(10);
            for (int i = 0; i < Recycler.Items().Count; i++)
            {
                FolderItem FI = Recycler.Items().Item(i);
                string FileName = Recycler.GetDetailsOf(FI, 0);
                if (Path.GetExtension(FileName) == "") FileName += Path.GetExtension(FI.Path);
                //Necessary for systems with hidden file extensions.
                string FilePath = Recycler.GetDetailsOf(FI, 1);
                if (Item == Path.Combine(FilePath, FileName))
                {
                    return DoVerb(FI, "ESTORE");
                }
            }
            return false;
        }

        private bool DoVerb(FolderItem Item, string Verb)
        {
            foreach (FolderItemVerb FIVerb in Item.Verbs())
            {
                if (FIVerb.Name.ToUpper().Contains(Verb.ToUpper()))
                {
                    FIVerb.DoIt();
                    return true;
                }
            }
            return false;
        }
    }
}