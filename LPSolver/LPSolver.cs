using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace LPSolver
{
    class LPSolver
    {
        private const double Epsilon = 0.00000001;
        private int sCount = 1, rCount = 1, wCount = 1;
        private int totalIter = 0;
        private int currentIter = 0;
        private int fileCount = 1;
        private string Path { get; set; }
        private string Name { get; set; }
        private List<string[]> ChangedToW { get; set; }
        private bool MaxMin { get; set; }
        private int SCount { get { return sCount; } set { sCount = value; } }
        private int RCount { get { return rCount; } set { rCount = value; } }
        private int WCount { get { return wCount; } set { wCount = value; } }
        private int FileCount { get { return fileCount; } set { fileCount = value; } }
        private int TotalIter { get { return totalIter; } set { totalIter = value; } }
        private int CurrentIter { get { return currentIter; } set { currentIter = value; } }
        private List<List<string>> Limits { get; set; }
        private FormattedLimits everything;
        private struct FormattedLimits
        {
            public List<string> names;
            public double[,] values;
        }
        public LPSolver(string path)
        {
            Path = path;
        }
        /// <summary>
        /// Reads the file, standardize the input, and solves the linear problem given.
        /// </summary>
        /// <returns>Negative value on error.</returns>
        public int Solve()
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();
            // Read
            Limits = ReadMPSFile();
            if (Limits == null)
            {
                Console.WriteLine("File could not read properly.");
                return -1;
            }
            // Standardize
            ChangedToW = new List<string[]>();
            Standardize();
            // Turn it into matrix
            LimitsToMatrix();
            if (everything.values == null || everything.names == null)
            {
                Console.WriteLine("A problem occured while parsing the file.");
                return -2;
            }
            // Solve
            string[,] sonuc = SimplexSolve();
            if (sonuc == null)
            {
                timer.Stop();
                WriteToAndOpenFile(TotalIter, TimeSpan.FromMilliseconds(timer.ElapsedMilliseconds)); // http://stackoverflow.com/a/32341814/
                return -3;
            }
            timer.Stop();
            WriteToAndOpenFile(sonuc, TotalIter, TimeSpan.FromMilliseconds(timer.ElapsedMilliseconds)); // http://stackoverflow.com/a/32341814/
            return 0;
        }
        /// <summary>
        /// Prints the given iteration
        /// </summary>
        /// <param name="iterationNo">Iteration number</param>
        /// <returns>Negative if an error occured.</returns>
        public int PrintIteration(int iterationNo)
        {
            if (iterationNo <= 0 || iterationNo > TotalIter)
            {
                Console.WriteLine("Invalid iteration number.");
                return -1;
            }
            SimplexSolve(iterationNo);
            return 0;
        }
        /// <summary>
        /// Reads and MPS file and returns it as a List of List of strings.
        /// </summary>
        /// <returns>null if an error occured, else in the first element the objective function, the rest are limits.</returns>
        private List<List<string>> ReadMPSFile()
        {
            int limnum = 0;
            if (!File.Exists(Path))
            {
                Console.WriteLine("File doesn't exist.");
                return null;
            }
            List<string> readText;
            try
            {
                readText = new List<string>(File.ReadAllLines(Path));
            }
            catch
            {
                Console.WriteLine("File read error.");
                return null;
            }
            readText.RemoveAll((string a) => a == "" ? false : a[0] == '*');
            List<List<string>> lpFormat = new List<List<string>>();
            lpFormat.Add(new List<string>());

            int index = readText.FindIndex((string a) => a.ToUpper().StartsWith("NAME"));
            if (index >= 0)
            {
                string[] temp = readText[index].Split((string[])null, StringSplitOptions.RemoveEmptyEntries);
                if (temp.Length < 2)
                {
                    Name = "Problem";
                }
                else
                {
                    Name = temp[1];
                }
            }

            // OBJECTIVE FUNCTION
            index = readText.FindIndex((string a) => a.ToUpper() == "OBJSENSE");
            if (index >= 0)
            {
                if (readText[index + 1].ToUpper() == " MAX" || readText[index + 1].ToUpper() == " MAXIMIZE")
                {
                    lpFormat[limnum].Add("MAX:");
                    MaxMin = true;
                }
                else
                {
                    lpFormat[limnum].Add("MIN:");
                    MaxMin = false;
                }
            }
            else
            {
                lpFormat[limnum].Add("MIN:");
                MaxMin = false;
            }

            index = readText.FindIndex((string a) => a.ToUpper() == "ROWS");
            if (index < 0)
            {
                Console.WriteLine("Couldn't find ROWS.");
                return null;
            }
            string objective = null;
            for (int i = index + 1; i < readText.Count; i++)
            {
                if (readText[i][0] != ' ')
                {
                    break;
                }
                if (readText[i][1] == 'N')
                {
                    string[] temp = readText[i].Split((string[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (temp.Length != 2)
                    {
                        Console.WriteLine("Wrong ROWS input (An error occured while finding the objective function's name.");
                        return null;
                    }

                    objective = temp[1];
                    readText.RemoveAt(i);
                    break;
                }
            }
            if (objective == null)
            {
                Console.WriteLine("Couldn't find objective function.");
                return null;
            }

            index = readText.FindIndex((string a) => a.ToUpper() == "COLUMNS");
            if (index < 0)
            {
                Console.WriteLine("Couldn't find COLUMNS.");
                return null;
            }
            for (int i = index + 1; i < readText.Count; i++)
            {
                if (readText[i][0] != ' ')
                {
                    break;
                }
                string[] temp = readText[i].Split((string[])null, StringSplitOptions.RemoveEmptyEntries);
                for (int j = 1; j < temp.Length; j += 2)
                {
                    if (temp[j] != objective)
                    {
                        continue;
                    }
                    if (j + 1 < temp.Length)
                    {
                        lpFormat[limnum].Add("+");
                        // TODO DOUBLE CHECK
                        lpFormat[limnum].Add(temp[j + 1]);
                        lpFormat[limnum].Add("*");
                        lpFormat[limnum].Add("_" + temp[0]);
                    }
                    else
                    {
                        Console.WriteLine("Wrong COLUMNS input.");
                    }
                }
            }
            lpFormat[limnum].Add("=");
            lpFormat[limnum].Add("0");
            limnum++;

            // LIMITS
            index = readText.FindIndex((string a) => a.ToUpper() == "ROWS");
            // It is already checked, and confirmed to exist

            for (int i = index + 1; i < readText.Count; i++)
            {
                string limit = null;
                if (readText[i][0] != ' ')
                {
                    break;
                }

                string[] temp = readText[i].Split((string[])null, StringSplitOptions.RemoveEmptyEntries);
                if (temp.Length != 2)
                {
                    Console.WriteLine("Wrong ROWS input (An error occured while finding a limit's name.");
                    return null;
                }

                limit = temp[1];
                lpFormat.Add(new List<string>());
                lpFormat[limnum].Add(limit + ":");

                index = readText.FindIndex((string a) => a.ToUpper() == "COLUMNS");
                // It is already checked, and confirmed to exist
                for (int j = index + 1; j < readText.Count; j++)
                {
                    if (readText[j][0] != ' ')
                    {
                        break;
                    }
                    temp = readText[j].Split((string[])null, StringSplitOptions.RemoveEmptyEntries);
                    for (int k = 1; k < temp.Length; k += 2)
                    {
                        if (temp[k] != limit)
                        {
                            continue;
                        }
                        if (k + 1 < temp.Length)
                        {
                            lpFormat[limnum].Add("+");
                            // TODO DOUBLE CHECK
                            lpFormat[limnum].Add(temp[k + 1]);
                            lpFormat[limnum].Add("*");
                            lpFormat[limnum].Add("_" + temp[0]);
                        }
                        else
                        {
                            Console.WriteLine("Wrong COLUMNS input.");
                        }
                    }
                }

                if (readText[i][1] == 'E')
                {
                    lpFormat[limnum].Add("=");
                }
                else if (readText[i][1] == 'L')
                {
                    lpFormat[limnum].Add("<=");
                }
                else if (readText[i][1] == 'G')
                {
                    lpFormat[limnum].Add(">=");
                }
                else
                {
                    Console.WriteLine("ROWS input is not supported.");
                    return null;
                }
                index = readText.FindIndex((string a) => a.ToUpper() == "RHS");
                bool flag = false;
                if (index < 0)
                {
                    Console.WriteLine("Couldn't find RHS.");
                }
                else
                {
                    for (int j = index + 1; j < readText.Count; j++)
                    {
                        if (readText[j][0] != ' ')
                        {
                            break;
                        }
                        temp = readText[j].Split((string[])null, StringSplitOptions.RemoveEmptyEntries);
                        for (int k = 1; k < temp.Length; k += 2)
                        {
                            if (temp[k] != limit)
                            {
                                continue;
                            }
                            if (k + 1 < temp.Length)
                            {
                                lpFormat[limnum].Add(temp[k + 1]);
                                flag = true;
                                break;
                            }
                            else
                            {
                                Console.WriteLine("Wrong RHS input.");
                            }
                        }
                        if (flag == true)
                        {
                            break;
                        }
                    }
                }
                if (flag == false)
                {
                    lpFormat[limnum].Add("0");
                }
                limnum++;
            }

            // BOUNDS
            index = readText.FindIndex((string a) => a.ToUpper() == "BOUNDS");
            if (index < 0)
            {
                Console.WriteLine("Couldn't find BOUNDS.");
            }
            else
            {
                string var = null;
                for (int i = index + 1; i < readText.Count; i++)
                {
                    if (readText[i][0] != ' ')
                    {
                        break;
                    }
                    string[] temp = readText[i].Split((string[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (temp.Length != 3 && temp.Length != 4)
                    {
                        Console.WriteLine("Wrong BOUNDS input (An error occured while finding a variable's bound.)");
                        return null;
                    }
                    var = temp[2];

                    if (temp[0] == "UP")
                    {
                        if (temp.Length != 4)
                        {
                            Console.WriteLine("Wrong BOUNDS UP input (There must be 4 fields.)");
                            return null;
                        }
                        lpFormat.Add(new List<string>());
                        // TODO DOUBLE CHECK
                        lpFormat[limnum].Add("+");
                        lpFormat[limnum].Add("1.0");
                        lpFormat[limnum].Add("*");
                        lpFormat[limnum].Add("_" + var);
                        lpFormat[limnum].Add("<=");
                        lpFormat[limnum].Add(temp[3]);
                    }
                    else if (temp[0] == "LO")
                    {
                        if (temp.Length != 4)
                        {
                            Console.WriteLine("Wrong BOUNDS LO input (There must be 4 fields.)");
                            return null;
                        }
                        lpFormat.Add(new List<string>());
                        // TODO DOUBLE CHECK
                        lpFormat[limnum].Add("+");
                        lpFormat[limnum].Add("1.0");
                        lpFormat[limnum].Add("*");
                        lpFormat[limnum].Add("_" + var);
                        lpFormat[limnum].Add(">=");
                        lpFormat[limnum].Add(temp[3]);
                    }
                    else if (temp[0] == "FX")
                    {
                        if (temp.Length != 4)
                        {
                            Console.WriteLine("Wrong BOUNDS FX input (There must be 4 fields.)");
                            return null;
                        }
                        lpFormat.Add(new List<string>());
                        // TODO DOUBLE CHECK
                        lpFormat[limnum].Add("+");
                        lpFormat[limnum].Add("1.0");
                        lpFormat[limnum].Add("*");
                        lpFormat[limnum].Add("_" + var);
                        lpFormat[limnum].Add("=");
                        lpFormat[limnum].Add(temp[3]);
                    }
                    else if (temp[0] == "FR")
                    {
                        if (temp.Length != 3)
                        {
                            Console.WriteLine("Wrong BOUNDS FR input (There must be 4 fields.)");
                            return null;
                        }
                        foreach (List<string> limit in lpFormat)
                        {
                            if (limit.Contains("_" + var))
                            {
                                index = limit.IndexOf("_" + var);
                                if (index >= 0)
                                {
                                    double d;
                                    if (Double.TryParse(limit[index - 2], out d))
                                    {
                                        if (d > 0)
                                        {
                                            limit.RemoveAt(index);
                                            limit.Insert(index, "+");
                                            limit.Insert(index + 1, limit[index - 2]);
                                            limit.Insert(index + 2, "*");
                                            limit.Insert(index + 3, "_" + var + "P");
                                            limit.Insert(index + 4, "+");
                                            limit.Insert(index + 5, "-" + limit[index - 2]);
                                            limit.Insert(index + 6, "*");
                                            limit.Insert(index + 7, "_" + var + "M");
                                            limit.RemoveAt(index - 1);
                                            limit.RemoveAt(index - 2);
                                            limit.RemoveAt(index - 3);
                                        }
                                        else
                                        {
                                            limit.RemoveAt(index);
                                            limit.Insert(index, "+");
                                            limit.Insert(index + 1, "-" + limit[index - 2]);
                                            limit.Insert(index + 2, "*");
                                            limit.Insert(index + 3, "_" + var + "P");
                                            limit.Insert(index + 4, "+");
                                            limit.Insert(index + 5, limit[index - 2]);
                                            limit.Insert(index + 6, "*");
                                            limit.Insert(index + 7, "_" + var + "M");
                                            limit.RemoveAt(index - 1);
                                            limit.RemoveAt(index - 2);
                                            limit.RemoveAt(index - 3);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Wrong COLUMNS input (the 3rd or the 5th field should be a number.)");
                                        return null;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine(temp[0] + "BOUND type is not supported.");
                        continue;
                    }
                    limnum++;
                }
            }
            return lpFormat;
        }
        /// <summary>
        /// Standardize all limits.
        /// </summary>
        private void Standardize()
        {
            int index = 0;
            List<int> boundsToRemove = new List<int>();
            foreach (List<string> limit in Limits)
            {
                if (limit[0] == "MAX:" || limit[0] == "MIN:")
                {
                    continue;
                }
                else
                {
                    if (limit.Contains("="))
                    {
                        index = limit.IndexOf("=");
                        limit.Insert(index, "+");
                        limit.Insert(index + 1, "1.0");
                        limit.Insert(index + 2, "*");
                        limit.Insert(index + 3, "R" + RCount.ToString());

                        Limits[0].Insert(Limits[0].Count - 2, "+");
                        Limits[0].Insert(Limits[0].Count - 2, "1.0");
                        Limits[0].Insert(Limits[0].Count - 2, "*");
                        Limits[0].Insert(Limits[0].Count - 2, "R" + RCount.ToString());

                        RCount++;
                    }
                    else if (limit.Contains("<="))
                    {
                        index = limit.IndexOf("<=");
                        limit.RemoveAt(index);
                        limit.Insert(index, "+");
                        limit.Insert(index + 1, "1.0");
                        limit.Insert(index + 2, "*");
                        limit.Insert(index + 3, "S" + SCount.ToString());
                        limit.Insert(index + 4, "=");
                        SCount++;
                    }
                    else if (limit.Contains(">="))
                    {
                        index = limit.IndexOf(">=");
                        double d;
                        if (Double.TryParse(limit[index + 1], out d))
                        {
                            if (d < 0)
                            {
                                string termToRemove = limit[index - 1];
                                limit[index + 1] = "0";
                                limit[index - 1] = "W" + WCount.ToString();
                                ChangedToW.Add(new string[3]);
                                ChangedToW[ChangedToW.Count - 1][0] = termToRemove;
                                ChangedToW[ChangedToW.Count - 1][1] = "W" + WCount.ToString();
                                ChangedToW[ChangedToW.Count - 1][2] = d.ToString();
                                WCount++;
                                foreach (List<string> limitInside in Limits)
                                {
                                    for (int i = 0; i < limitInside.Count; i++)
                                    {
                                        if (limitInside[i] == termToRemove)
                                        {
                                            double d2;
                                            if (Double.TryParse(limitInside[i - 2], out d2))
                                            {
                                                double d3;
                                                if (Double.TryParse(limitInside[limitInside.Count - 1], out d3))
                                                {
                                                    limitInside[limitInside.Count - 1] = (d3 + d2 * -d).ToString();
                                                    limitInside[i] = ChangedToW[ChangedToW.Count - 1][1];
                                                }
                                            }
                                            else
                                            {
                                                Console.WriteLine("Wrong RHS input (the 3rd or the 5th field should be a number.)");
                                            }
                                        }
                                    }
                                }
                                boundsToRemove.Add(Limits.IndexOf(limit));
                            }
                            else
                            {
                                limit.RemoveAt(index);
                                limit.Insert(index, "+");
                                limit.Insert(index + 1, "-1.0");
                                limit.Insert(index + 2, "*");
                                limit.Insert(index + 3, "S" + SCount.ToString());
                                limit.Insert(index + 4, "+");
                                limit.Insert(index + 5, "1.0");
                                limit.Insert(index + 6, "*");
                                limit.Insert(index + 7, "R" + RCount.ToString());
                                limit.Insert(index + 8, "=");

                                Limits[0].Insert(Limits[0].Count - 2, "+");
                                Limits[0].Insert(Limits[0].Count - 2, "1.0");
                                Limits[0].Insert(Limits[0].Count - 2, "*");
                                Limits[0].Insert(Limits[0].Count - 2, "R" + RCount.ToString());

                                SCount++;
                                RCount++;
                            }
                        }
                    }
                }
            }
            boundsToRemove.Sort();
            boundsToRemove.Reverse(); // descending
            foreach (int bound in boundsToRemove)
            {
                Limits.RemoveAt(bound);
            }
        }
        /// <summary>
        /// Uses the output of Standardize method and creates a variable names array and a 2d double matrix that contains the values of the variables.
        /// </summary>
        private void LimitsToMatrix()
        {
            everything = new FormattedLimits();
            everything.names = new List<string>();

            foreach (List<string> limit in Limits)
            {
                for (int i = 0; i < limit.Count; i++)
                {
                    if (limit[i] == "+")
                    {
                        if (!everything.names.Contains(limit[i + 3]))
                        {
                            everything.names.Add(limit[i + 3]);
                        }
                    }
                }
            }
            everything.names.Add("_RHS");
            everything.values = new double[Limits.Count, everything.names.Count];
            List<string> objective = null;
            int index;
            for (int i = 0; i < Limits.Count; i++)
            {
                if (Limits[i][0] != "MAX:" && Limits[i][0] != "MIN:")
                {
                    for (int j = 0; j < Limits[i].Count; j++)
                    {
                        if (Limits[i][j] == "+")
                        {
                            if (everything.names.Contains(Limits[i][j + 3]))
                            {
                                index = everything.names.IndexOf(Limits[i][j + 3]);
                                double d;
                                if (Double.TryParse(Limits[i][j + 1], out d))
                                {
                                    everything.values[i - 1, index] = d;
                                }
                                else
                                {
                                    Console.WriteLine("Wrong COLUMNS input (the 3rd or the 5th field should be a number.)");
                                    everything.names = null;
                                    everything.values = null;
                                    return;
                                }
                            }
                        }
                        if (Limits[i][j] == "=")
                        {
                            index = everything.names.IndexOf("_RHS");
                            double d;
                            if (Double.TryParse(Limits[i][j + 1], out d))
                            {
                                everything.values[i - 1, index] = d;
                            }
                            else
                            {
                                Console.WriteLine("Wrong RHS input (the 3rd or the 5th field should be a number.)");
                                everything.names = null;
                                everything.values = null;
                                return;
                            }
                        }
                    }
                }
                else
                {
                    objective = Limits[i];
                }
            }
            for (int i = 0; i < objective.Count; i++)
            {
                if (objective[i] == "+")
                {
                    if (everything.names.Contains(objective[i + 3]))
                    {
                        index = everything.names.IndexOf(objective[i + 3]);
                        double d;
                        if (Double.TryParse(objective[i + 1], out d))
                        {
                            everything.values[everything.values.GetLength(0) - 1, index] = -d;
                        }
                        else
                        {
                            Console.WriteLine("Wrong OBJECTIVE COLUMNS input (the 3rd or the 5th field should be a number.)");
                            everything.names = null;
                            everything.values = null;
                            return;
                        }
                    }
                }
                if (objective[i] == "=")
                {
                    double d;
                    if (Double.TryParse(objective[i + 1], out d))
                    {
                        everything.values[everything.values.GetLength(0) - 1, everything.values.GetLength(1) - 1] = -d;
                    }
                }
            }
        }
        private string[,] SimplexSolve()
        {
            List<string> names = new List<string>();
            foreach (string name in everything.names)
            {
                names.Add(name);
            }
            int m = everything.values.GetLength(0);
            int n = everything.values.GetLength(1);
            int yapay = 0, artikDolgu = 0;

            string[] satirAdi = new string[m];
            satirAdi[m - 1] = "z";

            // Hangi satır hangi değişkene ait?
            bool birim;
            int birlerinSayisi;

            int satir = 0, sutun = 0;

            for (int i = 0; i < n; i++)
            {
                birim = true;
                birlerinSayisi = 0;
                for (int j = 0; j < m - 1; j++)
                {
                    if (!(everything.values[j, i] == 0 || everything.values[j, i] == 1))
                    {
                        birim = false;
                        break;
                    }

                    if (everything.values[j, i] == 1)
                    {
                        birlerinSayisi++;
                        satir = j;
                        sutun = i;
                    }
                }

                if (birim && birlerinSayisi == 1)
                {
                    satirAdi[satir] = names[sutun];
                }
            }

            bool[] yapayMi = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (names[i].ToUpper().ToCharArray()[0].Equals('R'))
                {
                    yapayMi[i] = true;
                    yapay++;
                }
                else
                {
                    yapayMi[i] = false;
                }

                if (names[i].ToUpper().ToCharArray()[0].Equals('S'))
                {
                    artikDolgu++;
                }
            }
            double[,] sonucMatrisi = new double[m, n - yapay];
            if (yapay > Epsilon)
            {
                double[,] matrisAsama1 = new double[m, n];

                // r satirini duzenle.
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < m - 1; j++)
                    {
                        if (names[i].ToUpper().ToCharArray()[0].Equals('R'))
                            matrisAsama1[m - 1, i] = 0;
                        else
                        {
                            if (satirAdi[j].ToUpper().ToCharArray()[0].Equals('R'))
                            {
                                matrisAsama1[m - 1, i] += everything.values[j, i];
                            }
                        }
                    }
                }
                satirAdi[m - 1] = "r";

                for (int i = 0; i < m - 1; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        matrisAsama1[i, j] = everything.values[i, j];
                    }
                }

                TotalIter++;

                // 1. asama
                matrisAsama1 = SimplexInner(matrisAsama1, names, satirAdi, false);
                if (matrisAsama1 == null)
                {
                    Console.WriteLine("An error occured.");
                    return null;
                }

                double[,] matrisAsama2 = new double[m, n - yapay];

                // Yapay degiskenleri at.
                bool[] normalDegisken = new bool[n];

                for (int i = 0; i < n; i++)
                {
                    if (!names[i].ToUpper().ToCharArray()[0].Equals('R'))
                    {
                        normalDegisken[i] = true;
                    }
                    else
                    {
                        normalDegisken[i] = false;
                    }
                }

                for (int i = 0, k = 0; i < n; i++)
                {
                    if (normalDegisken[i] == false)
                    {
                        continue;
                    }
                    else
                    {
                        for (int j = 0; j < m; j++)
                        {
                            matrisAsama2[j, k] = matrisAsama1[j, i];
                        }
                        k++;
                    }
                }

                for (int i = 0, k = 0; i < n - yapay - 1; i++)
                {
                    if (normalDegisken[i] == false)
                    {
                        continue;
                    }
                    else
                    {
                        matrisAsama2[m - 1, i] = everything.values[m - 1, k];
                    }
                    k++;
                }

                for (int i = 0; i < m - 1; i++)
                {
                    matrisAsama2[i, n - yapay - 1] = matrisAsama1[i, n - 1];
                }

                matrisAsama2[m - 1, n - yapay - 1] = everything.values[m - 1, n - 1];
                names.RemoveAll((string a) => a[0] == 'R');

                names[n - yapay - 1] = "RHS";

                satirAdi[m - 1] = "z*";

                // z satirini duzenle.
                double[,] temp = new double[m, n - yapay];
                int index = 0;
                for (int i = 0; i < m - 1; i++)
                {
                    if (i < n - yapay)
                    {
                        index = names.IndexOf(satirAdi[i]);
                        if (index < 0)
                        {
                            PrintMatrix(matrisAsama2, names, satirAdi);
                            Console.WriteLine("There is a {0} element left in the left side.", satirAdi[i]);
                            return null;
                        }
                    }

                    for (int j = 0; j < n - yapay; j++)
                    {
                        temp[i, j] = matrisAsama2[i, j];
                        temp[i, j] *= everything.values[m - 1, index] * -1;
                    }
                }

                for (int i = 0; i < n - yapay; i++)
                {
                    for (int j = 0; j < m - 1; j++)
                    {
                        matrisAsama2[m - 1, i] += temp[j, i];
                    }
                }

                satirAdi[m - 1] = "z";

                // 2. aşama
                sonucMatrisi = SimplexInner(matrisAsama2, names, satirAdi, MaxMin);
                if (sonucMatrisi == null)
                {
                    Console.WriteLine("An error occured.");
                    return null;
                }
            }
            else // hiç yapay değişken kullanılmamış ise
            {
                satirAdi[m - 1] = "z";
                sonucMatrisi = SimplexInner(everything.values, names, satirAdi, MaxMin);
                if (sonucMatrisi == null)
                {
                    Console.WriteLine("An error occured.");
                    return null;
                }
            }

            string[,] sonuclar = new string[m, 2];
            for (int i = 0; i < m; i++)
            {
                sonuclar[i, 0] = satirAdi[i];
                sonuclar[i, 1] = sonucMatrisi[i, n - yapay - 1].ToString();
            }

            return sonuclar;
        }
        private double[,] SimplexInner(double[,] values, List<string> varNames, string[] rowNames, bool max)
        {
            double enUzak; // z satirinda 0'a en uzak olan katsayi
            double enYakin; // sts sutunundaki degerler anahtar sutundaki karsilik gelen degerlere bolundugunde 0'a en yakin olan deger [0, INF)
            int anahtarSatir = 0, anahtarSutun = 0;
            double anahtarEleman;

            int m = values.GetLength(0);
            int n = values.GetLength(1);

            double[,] yeniMatris = new double[m, n];
            bool devam;

            if (max)
            {
                do
                {
                    devam = false;

                    enUzak = double.MaxValue;
                    for (int i = 0; i < n - 1; i++)
                    {
                        if (values[m - 1, i] < enUzak) // negatif sayilar icinde en kucuk olani
                        {
                            enUzak = values[m - 1, i];
                            anahtarSutun = i;
                        }
                    }
                    if (enUzak == double.MaxValue)
                    {
                        Console.WriteLine("Uygun giren degisken yok.");
                        return null;
                    }

                    enYakin = double.MaxValue;
                    for (int i = 0; i < m - 1; i++)
                    {
                        if (values[i, anahtarSutun] > Epsilon)
                        {
                            if ((values[i, n - 1] / values[i, anahtarSutun]) < enYakin)
                            {
                                enYakin = Math.Round(values[i, n - 1] / values[i, anahtarSutun], 10);
                                anahtarSatir = i;
                            }
                        }
                    }
                    if (enYakin == double.MaxValue)
                    {
                        Console.WriteLine("Uygun cikan degisken yok.");
                        return null;
                    }

                    anahtarEleman = values[anahtarSatir, anahtarSutun];

                    rowNames[anahtarSatir] = varNames[anahtarSutun];

                    for (int i = 0; i < n; i++)
                    {
                        yeniMatris[anahtarSatir, i] = Math.Round(values[anahtarSatir, i] / anahtarEleman, 10);
                    }

                    for (int i = 0; i < m; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            if (i != anahtarSatir)
                            {
                                yeniMatris[i, j] = Math.Round(yeniMatris[anahtarSatir, j] * values[i, anahtarSutun] * -1 + values[i, j], 10);
                            }
                        }
                    }

                    for (int i = 0; i < m; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            values[i, j] = yeniMatris[i, j];
                        }
                    }

                    TotalIter++;

                    for (int i = 0; i < n - 1; i++)
                    {
                        if (values[m - 1, i] < -Epsilon)
                        {
                            devam = true;
                            break;
                        }
                    }
                }
                while (devam);
            }
            else // minimum
            {
                do
                {
                    devam = false;

                    enUzak = double.MinValue;
                    for (int i = 0; i < n - 1; i++)
                    {
                        if (values[m - 1, i] > enUzak) // pozitif sayilar icinde en buyuk olani
                        {
                            enUzak = values[m - 1, i];
                            anahtarSutun = i;
                        }
                    }
                    if (enUzak == double.MinValue)
                    {
                        Console.WriteLine("Uygun giren degisken yok.");
                        return null;
                    }

                    enYakin = double.MaxValue;
                    for (int i = 0; i < m - 1; i++)
                    {
                        if (values[i, anahtarSutun] > Epsilon)
                        {
                            if ((values[i, n - 1] / values[i, anahtarSutun]) < enYakin)
                            {
                                enYakin = Math.Round(values[i, n - 1] / values[i, anahtarSutun], 10);
                                anahtarSatir = i;
                            }
                        }
                    }
                    if (enYakin == double.MaxValue)
                    {
                        Console.WriteLine("Uygun cikan degisken yok.");
                        return null;
                    }

                    anahtarEleman = values[anahtarSatir, anahtarSutun];

                    rowNames[anahtarSatir] = varNames[anahtarSutun];

                    for (int i = 0; i < n; i++)
                    {
                        yeniMatris[anahtarSatir, i] = Math.Round(values[anahtarSatir, i] / anahtarEleman, 10);
                    }

                    for (int i = 0; i < m; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            if (i != anahtarSatir)
                            {
                                yeniMatris[i, j] = Math.Round(yeniMatris[anahtarSatir, j] * values[i, anahtarSutun] * -1 + values[i, j], 10);
                            }
                        }
                    }

                    for (int i = 0; i < m; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            values[i, j] = yeniMatris[i, j];
                        }
                    }

                    TotalIter++;

                    for (int i = 0; i < n - 1; i++)
                    {
                        if (values[m - 1, i] > Epsilon)
                        {
                            devam = true;
                            break;
                        }
                    }
                }
                while (devam);
            }
            return values;
        }
        private void SimplexSolve(int iterationNo)
        {
            List<string> names = new List<string>();
            foreach (string name in everything.names)
            {
                names.Add(name);
            }
            CurrentIter = 0;
            int m = everything.values.GetLength(0);
            int n = everything.values.GetLength(1);
            int yapay = 0, artikDolgu = 0;

            string[] satirAdi = new string[m];
            satirAdi[m - 1] = "z";

            // Hangi satır hangi değişkene ait?
            bool birim;
            int birlerinSayisi;

            int satir = 0, sutun = 0;

            for (int i = 0; i < n; i++)
            {
                birim = true;
                birlerinSayisi = 0;
                for (int j = 0; j < m - 1; j++)
                {
                    if (!(everything.values[j, i] == 0 || everything.values[j, i] == 1))
                    {
                        birim = false;
                        break;
                    }

                    if (everything.values[j, i] == 1)
                    {
                        birlerinSayisi++;
                        satir = j;
                        sutun = i;
                    }
                }

                if (birim && birlerinSayisi == 1)
                {
                    satirAdi[satir] = names[sutun];
                }
            }

            if (CurrentIter == iterationNo)
            {
                PrintMatrix(everything.values, names, satirAdi);
                return;
            }

            bool[] yapayMi = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (names[i].ToUpper().ToCharArray()[0].Equals('R'))
                {
                    yapayMi[i] = true;
                    yapay++;
                }
                else
                {
                    yapayMi[i] = false;
                }

                if (names[i].ToUpper().ToCharArray()[0].Equals('S'))
                {
                    artikDolgu++;
                }
            }
            if (yapay > Epsilon)
            {
                double[,] matrisAsama1 = new double[m, n];

                // r satirini duzenle.
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < m - 1; j++)
                    {
                        if (names[i].ToUpper().ToCharArray()[0].Equals('R'))
                            matrisAsama1[m - 1, i] = 0;
                        else
                        {
                            if (satirAdi[j].ToUpper().ToCharArray()[0].Equals('R'))
                            {
                                matrisAsama1[m - 1, i] += everything.values[j, i];
                            }
                        }
                    }
                }
                satirAdi[m - 1] = "r";

                for (int i = 0; i < m - 1; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        matrisAsama1[i, j] = everything.values[i, j];
                    }
                }

                CurrentIter++;
                if (CurrentIter == iterationNo)
                {
                    PrintMatrix(matrisAsama1, names, satirAdi);
                    return;
                }

                // 1. asama
                matrisAsama1 = SimplexInner(matrisAsama1, names, satirAdi, false, iterationNo);
                if (matrisAsama1 == null)
                {
                    return;
                }

                double[,] matrisAsama2 = new double[m, n - yapay];

                // Yapay degiskenleri at.
                bool[] normalDegisken = new bool[n];

                for (int i = 0; i < n; i++)
                {
                    if (!names[i].ToUpper().ToCharArray()[0].Equals('R'))
                    {
                        normalDegisken[i] = true;
                    }
                    else
                    {
                        normalDegisken[i] = false;
                    }
                }

                for (int i = 0, k = 0; i < n; i++)
                {
                    if (normalDegisken[i] == false)
                    {
                        continue;
                    }
                    else
                    {
                        for (int j = 0; j < m; j++)
                        {
                            matrisAsama2[j, k] = matrisAsama1[j, i];
                        }
                        k++;
                    }
                }

                for (int i = 0, k = 0; i < n - yapay - 1; i++)
                {
                    if (normalDegisken[i] == false)
                    {
                        continue;
                    }
                    else
                    {
                        matrisAsama2[m - 1, i] = everything.values[m - 1, k];
                    }
                    k++;
                }

                for (int i = 0; i < m - 1; i++)
                {
                    matrisAsama2[i, n - yapay - 1] = matrisAsama1[i, n - 1];
                }

                matrisAsama2[m - 1, n - yapay - 1] = everything.values[m - 1, n - 1];
                names.RemoveAll((string a) => a[0] == 'R');

                names[n - yapay - 1] = "RHS";

                satirAdi[m - 1] = "z*";

                if (CurrentIter == iterationNo)
                {
                    PrintMatrix(matrisAsama2, names, satirAdi);
                    return;
                }

                // z satirini duzenle.
                double[,] temp = new double[m, n - yapay];
                int index = 0;
                for (int i = 0; i < m - 1; i++)
                {
                    if (i < n - yapay)
                    {
                        index = names.IndexOf(satirAdi[i]);
                    }

                    for (int j = 0; j < n - yapay; j++)
                    {
                        temp[i, j] = matrisAsama2[i, j];
                        temp[i, j] *= everything.values[m - 1, index] * -1;
                    }
                }

                for (int i = 0; i < n - yapay; i++)
                {
                    for (int j = 0; j < m - 1; j++)
                    {
                        matrisAsama2[m - 1, i] += temp[j, i];
                    }
                }

                satirAdi[m - 1] = "z";

                if (CurrentIter == iterationNo)
                {
                    PrintMatrix(matrisAsama2, names, satirAdi);
                    return;
                }

                // 2. aşama
                if (SimplexInner(matrisAsama2, names, satirAdi, MaxMin, iterationNo) == null)
                {
                    return;
                }
            }
            else // hiç yapay değişken kullanılmamış ise
            {
                satirAdi[m - 1] = "z";
                if (SimplexInner(everything.values, names, satirAdi, MaxMin, iterationNo) == null)
                {
                    return;
                }
            }
        }
        private double[,] SimplexInner(double[,] values, List<string> varNames, string[] rowNames, bool max, int iterationNo)
        {
            double enUzak; // z satirinda 0'a en uzak olan katsayi
            double enYakin; // sts sutunundaki degerler anahtar sutundaki karsilik gelen degerlere bolundugunde 0'a en yakin olan deger [0, INF)
            int anahtarSatir = 0, anahtarSutun = 0;
            double anahtarEleman;

            int m = values.GetLength(0);
            int n = values.GetLength(1);

            double[,] yeniMatris = new double[m, n];
            bool devam;

            if (max)
            {
                do
                {
                    devam = false;

                    enUzak = double.MaxValue;
                    for (int i = 0; i < n - 1; i++)
                    {
                        if (values[m - 1, i] < enUzak) // negatif sayilar icinde en kucuk olani
                        {
                            enUzak = values[m - 1, i];
                            anahtarSutun = i;
                        }
                    }
                    if (enUzak == double.MaxValue)
                    {
                        Console.WriteLine("Uygun giren degisken yok.");
                        return null;
                    }

                    enYakin = double.MaxValue;
                    for (int i = 0; i < m - 1; i++)
                    {
                        if (values[i, anahtarSutun] > Epsilon)
                        {
                            if ((values[i, n - 1] / values[i, anahtarSutun]) < enYakin)
                            {
                                enYakin = Math.Round(values[i, n - 1] / values[i, anahtarSutun], 10);
                                anahtarSatir = i;
                            }
                        }
                    }
                    if (enYakin == double.MaxValue)
                    {
                        Console.WriteLine("Uygun cikan degisken yok.");
                        return null;
                    }

                    anahtarEleman = values[anahtarSatir, anahtarSutun];

                    rowNames[anahtarSatir] = varNames[anahtarSutun];

                    for (int i = 0; i < n; i++)
                    {
                        yeniMatris[anahtarSatir, i] = Math.Round(values[anahtarSatir, i] / anahtarEleman, 10);
                    }

                    for (int i = 0; i < m; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            if (i != anahtarSatir)
                            {
                                yeniMatris[i, j] = Math.Round(yeniMatris[anahtarSatir, j] * values[i, anahtarSutun] * -1 + values[i, j], 10);
                            }
                        }
                    }

                    for (int i = 0; i < m; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            values[i, j] = yeniMatris[i, j];
                        }
                    }

                    CurrentIter++;
                    if (CurrentIter == iterationNo)
                    {
                        PrintMatrix(values, varNames, rowNames);
                        return null;
                    }

                    for (int i = 0; i < n - 1; i++)
                    {
                        if (values[m - 1, i] < -Epsilon)
                        {
                            devam = true;
                            break;
                        }
                    }
                }
                while (devam);
            }
            else // minimum
            {
                do
                {
                    devam = false;

                    enUzak = double.MinValue;
                    for (int i = 0; i < n - 1; i++)
                    {
                        if (values[m - 1, i] > enUzak) // pozitif sayilar icinde en buyuk olani
                        {
                            enUzak = values[m - 1, i];
                            anahtarSutun = i;
                        }
                    }
                    if (enUzak == double.MinValue)
                    {
                        Console.WriteLine("Uygun giren degisken yok.");
                        return null;
                    }

                    enYakin = double.MaxValue;
                    for (int i = 0; i < m - 1; i++)
                    {
                        if (values[i, anahtarSutun] > Epsilon)
                        {
                            if ((values[i, n - 1] / values[i, anahtarSutun]) < enYakin)
                            {
                                enYakin = Math.Round(values[i, n - 1] / values[i, anahtarSutun], 10);
                                anahtarSatir = i;
                            }
                        }
                    }
                    if (enYakin == double.MaxValue)
                    {
                        Console.WriteLine("Uygun cikan degisken yok.");
                        return null;
                    }

                    anahtarEleman = values[anahtarSatir, anahtarSutun];

                    rowNames[anahtarSatir] = varNames[anahtarSutun];

                    for (int i = 0; i < n; i++)
                    {
                        yeniMatris[anahtarSatir, i] = Math.Round(values[anahtarSatir, i] / anahtarEleman, 10);
                    }

                    for (int i = 0; i < m; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            if (i != anahtarSatir)
                            {
                                yeniMatris[i, j] = Math.Round(yeniMatris[anahtarSatir, j] * values[i, anahtarSutun] * -1 + values[i, j], 10);
                            }
                        }
                    }

                    for (int i = 0; i < m; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            values[i, j] = yeniMatris[i, j];
                        }
                    }

                    CurrentIter++;
                    if (CurrentIter == iterationNo)
                    {
                        PrintMatrix(values, varNames, rowNames);
                        return null;
                    }

                    for (int i = 0; i < n - 1; i++)
                    {
                        if (values[m - 1, i] > Epsilon)
                        {
                            devam = true;
                            break;
                        }
                    }
                }
                while (devam);
            }
            return values;
        }
        private void PrintMatrix(double[,] values, List<string> varNames, string[] rowNames)
        {
            int m = values.GetLength(0);
            int n = values.GetLength(1);

            Console.Write("\t");
            for (int i = 0; i < n; i++)
                Console.Write("{0, 10: 0.000}", varNames[i]);
            Console.WriteLine();

            for (int i = 0; i < m; i++)
            {
                Console.Write(rowNames[i] + "\t");

                for (int j = 0; j < n; j++)
                    Console.Write("{0, 10: 0.000}", values[i, j]);
                Console.WriteLine();
            }
            Console.WriteLine();
        }
        /// <summary>
        /// Uses the answer line, total iteration number and the timespan of the problem and writes them to a file.
        /// </summary>
        /// <param name="answer">The solution of the objective function (Z line)</param>
        /// <param name="iteration">Total number of iterations</param>
        /// <param name="ts">TimeSpan of the problem to solve</param>
        /// <returns>Negative value on error.</returns>
        private int WriteToAndOpenFile(string[,] answer, int iteration, TimeSpan ts)
        {
            fileCount = 0;
            while (File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Name + " Solution " + FileCount.ToString() + ".txt")))
            {
                FileCount++;
            }
            try
            {
                using (StreamWriter file =
                    new StreamWriter(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Name + " Solution " + FileCount.ToString() + ".txt"))) // https://msdn.microsoft.com/en-us/library/8bh11f1k.aspx
                {
                    file.WriteLine(Name);
                    file.WriteLine();
                    file.WriteLine("The solution took {0} iterations.", iteration);
                    file.WriteLine("The solution took {0}.", ts.ToString());
                    for (int i = 0; i < answer.GetLength(0); i++)
                    {
                        if (answer[i, 0][0] == 'z')
                        {
                            foreach (string[] w in ChangedToW)
                            {
                                bool answerContainsW = false;
                                for (int j = 0; j < answer.GetLength(0); j++)
                                {
                                    if (w[1] == answer[j, 0])
                                    {
                                        answerContainsW = true;
                                    }
                                }
                                if (!answerContainsW)
                                {
                                    file.WriteLine("{0} = {1}", w[0].Substring(1).ToUpper(), w[2]);
                                }
                            }
                            file.WriteLine("{0} = {1}", answer[i, 0].ToUpper(), answer[i, 1]);
                        }
                        else if (answer[i, 0][0] == 'W')
                        {
                            for (int j = 0; j < ChangedToW.Count; j++)
                            {
                                if (ChangedToW[j][1] == answer[i, 0])
                                {
                                    double d1, d2;
                                    if (Double.TryParse(answer[i, 1], out d1) && Double.TryParse(ChangedToW[j][2], out d2))
                                    {
                                        file.WriteLine("{0} = {1}", ChangedToW[j][0].Substring(1).ToUpper(), d1 + d2);
                                    }
                                }
                            }
                        }
                        else if (answer[i, 0][0] == '_')
                        {
                            file.WriteLine("{0} = {1}", answer[i, 0].Substring(1).ToUpper(), answer[i, 1]);
                        }
                        else
                        {
                            file.WriteLine("{0} = {1}", answer[i, 0].ToUpper(), answer[i, 1]);
                        }
                    }
                }
                try
                {
                    Process.Start(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Name + " Solution " + FileCount.ToString() + ".txt"));
                }
                catch
                {
                    Console.WriteLine("File read error.");
                    return -2;
                }
            }
            catch
            {
                Console.WriteLine("File write error.");
                return -1;
            }
            return 0;
        }
        /// <summary>
        /// Uses the total iteration number and the timespan of the problem and writes them to a file with a generic error.
        /// </summary>
        /// <param name="iteration">Total number of iterations</param>
        /// <param name="ts">TimeSpan of the problem to solve</param>
        /// <returns>Negative value on error.</returns>
        private int WriteToAndOpenFile(int iteration, TimeSpan ts)
        {
            FileCount = 0;
            while (File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Name + " Solution " + FileCount.ToString() + ".txt")))
            {
                FileCount++;
            }
            try
            {
                using (StreamWriter file =
                    new StreamWriter(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Name + " Solution " + FileCount.ToString() + ".txt"))) // https://msdn.microsoft.com/en-us/library/8bh11f1k.aspx
                {
                    file.WriteLine(Name);
                    file.WriteLine();
                    file.WriteLine("The solution took {0} iterations.", iteration);
                    file.WriteLine("The solution took {0}.", ts.ToString());
                    file.WriteLine("No optimal solution was found.");
                }
                try
                {
                    Process.Start(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Name + " Solution " + FileCount.ToString() + ".txt"));
                }
                catch
                {
                    Console.WriteLine("File read error.");
                    return -2;
                }
            }
            catch
            {
                Console.WriteLine("File write error.");
                return -1;
            }
            return 0;
        }
    }
}
