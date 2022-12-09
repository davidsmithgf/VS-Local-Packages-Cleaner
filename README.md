# VS-Local-Packages-Cleaner
Visual Studio installer local packages cache cleaner

(personal, not official, do not download or use if any unclear for you)
Tested for VS 2017, 2019, 2022 package folder.

Usage:
1) Set the timeline to avoid delete new files in appsettings.json -> AppSettings -> newfileTimeline;
2) Running the program (VS Local Packages Cleaner.exe) via command line;
3) Input packages cache folder full path;
4) Wait the program to generate a .cmd file for clean command, file name is [foldername]+[timestamp];
5) Click and run the newly generated [foldername]+[timestamp].cmd;

Notice:
1) Just using after each time visual studio upgrade, recognize & remove old version packages;
2) If you're not clear what to do for the .cmd file, please do NOT click or run the .cmd;
3) Tell me if any question or dissatisfaction;
