using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace reader
{
    public partial class Form1 : Form
    {
        // All tolerances must be non-zero.
        // GAG_TOLERANCE:       doubled error tolerance for combining GAGs of similar MW
        // SHIFT_TOLERANCE:     error tolerance for grouping GAGs by a mass shift
        // SEARCH_TOLERANCE:    error tolerance to allow when searching for a GAG of known MW

        const double GAG_TOLERANCE = 30.0 * 2.0;
        const double SHIFT_TOLERANCE = 2.5;
        const double SEARCH_TOLERANCE = 1.0;
        const double MATCH_TOLERANCE = 10.0;

        public Form1()
        {
            InitializeComponent();
        }
        

        private void lCMSDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog fDialog = new OpenFileDialog();
            fDialog.Title = "Open LCMS Data File";
            fDialog.Filter = "CSV Files|*.csv";
            if (fDialog.ShowDialog() == DialogResult.Cancel)
                return;

            try
            {
                this.fsInputLCMSFile = new FileStream(fDialog.FileName, FileMode.Open, FileAccess.Read);
                this.parseLCMSRun();
                
            }
            catch (Exception)
            {
                MessageBox.Show("Error opening file", "File Error",
                                 MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void hypListMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog fDialog = new OpenFileDialog();
            fDialog.Title = "Open Hypothetical List File";
            fDialog.Filter = "CSV Files|*.csv";
            if (fDialog.ShowDialog() == DialogResult.Cancel)
                return;

            this.hypList = new SortedList<GAG, List<string>>();
            string[] parts;
            string[] parts2;
            try
            {
                FileStream fsHypListFile = new FileStream(fDialog.FileName, FileMode.Open, FileAccess.Read);
                StreamReader rdr = new StreamReader(fsHypListFile);
                while (rdr.Peek() >= 0)
                {
                    string str = rdr.ReadLine();
                    parts = str.Split('"');
                    parts2 = parts[0].Split(',');
                    GAG compound = new GAG(Convert.ToDouble(parts2[1]),MATCH_TOLERANCE);
                    string key = parts[1];
                    int idx = this.hypList.IndexOfKey(compound);
                    if (idx >= 0)
                    {
                        this.hypList.ElementAt(idx).Value.Add(key);
                    }
                    else
                    {
                        List<string> li = new List<String>();
                        li.Add(key);
                        this.hypList.Add(compound, li);
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Error opening file", "File Error",
                                 MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private double Variance(List<double> list)
        {
            double avg = list.Average();
            double var = 0.0;
            foreach (double num in list)
            {
                var += Math.Pow((num - avg), 2);
            }
            return var;
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
           
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // Shift is key-value pair, where key is mass shift weight and value is 0 for do not combine, 1 for combine
        private void calMassShiftTargetted(SortedList<double, int> shifts)
        {
            SortedList<int, List<double>> groupings = new SortedList<int, List<double>>();
            groupings.Clear();
            int group_counter = 1;
            // Loop through all mass shift targets in order
            for (int s = 0; s < shifts.Count(); s++)
            {
                bool combine = true;
                if (shifts.Values[s] == 0)
                    combine = false;

                // For each MW, add the mass shift and see if there is a compound at the calculated MW
                for (int i = 0; i < this.sUnknownGAG.Count(); i++)
                {
                    GAG gag1 = this.sUnknownGAG.Keys[i];
                    GAG gag2 = new GAG(gag1.MolecularWeight + shifts.Keys[s], SHIFT_TOLERANCE);
                    int idx = this.sUnknownGAG.IndexOfKey(gag2);
                    
                    // If there is a match, mark them as the same group
                    if (idx >= 0)
                    {
                        gag2 = (GAG)this.sUnknownGAG.ElementAt(idx).Key;
                        if (gag1.group_flag > 0)
                        {
                            gag2.group_flag = gag1.group_flag;
                            groupings[gag1.group_flag].Add(gag2.MolecularWeight);
                        }
                        else
                        {
                            gag1.group_flag = group_counter;
                            groupings.Add(gag1.group_flag, new List<double>());
                            groupings[gag1.group_flag].Add(gag1.MolecularWeight);
                            groupings[gag1.group_flag].Add(gag2.MolecularWeight);
                            gag2.group_flag = group_counter++;
                        }
                    }
                }
                // Modify GAG list, grouping MWs based on group_flag
                foreach (var group in groupings)
                {
                    GAG rep = new GAG(group.Value.First(), SEARCH_TOLERANCE);
                    int rIdx = this.sUnknownGAG.IndexOfKey(rep);
                    rep = this.sUnknownGAG.Keys[rIdx];
                    if (combine)
                    {
                        KeyValuePair<double, int> mod = new KeyValuePair<double, int>(shifts.Keys[s], group.Value.Count);
                        rep.mModStates.Add(mod);
                    }
                    foreach (var mw in group.Value)
                    {
                        // The list is already in order
                        if (mw != group.Value.First())
                        {
                            GAG modified = new GAG(mw, SEARCH_TOLERANCE);
                            int mIdx = this.sUnknownGAG.IndexOfKey(modified);
                            if (mIdx < 0)
                            {
                                Console.Write("WARNING: " + modified.MolecularWeight + " appears to have been deleted already!\n");
                                continue;
                            }
                            modified = this.sUnknownGAG.Keys[mIdx];

                            if (combine)
                            {
                                // Scan numbers (elution times)
                                rep.mElutionTimes.AddRange(modified.mElutionTimes);

                                // Charge states
                                foreach (var charge in modified.mChargeState)
                                {
                                    if (rep.mChargeState.IndexOf(charge) < 0)
                                        rep.mChargeState.Add(charge);
                                }

                                // Volume
                                rep.mVolume.AddRange(modified.mVolume);
                                this.sUnknownGAG[rep] += this.sUnknownGAG[modified];

                                // Signal to Noise
                                rep.mSignalNoise.AddRange(modified.mSignalNoise);
                            }
                            this.sUnknownGAG.RemoveAt(mIdx);
                        }
                    }
                    if (combine)
                    {
                        // Recompute density
                        int RangeofElution = rep.mElutionTimes.Max() - rep.mElutionTimes.Min() + 1;
                        rep.mDensity = (double)rep.mElutionTimes.Count / (double)RangeofElution;
                    }
                }
            }
        }

        private bool parseLCMSRun()
        {
            this.sUnknownGAG = new SortedList<GAG, double>();
            this.data = new DataTable();
            for (int i = 0; i < 6; i++)
            {
                DataColumn col = new DataColumn();
                this.data.Columns.Add("" + i, typeof(double));
            }
            try
            {
                StreamReader rdr = new StreamReader(this.fsInputLCMSFile);
                //string wrd_name = this.fsInputLCMSFile.Name.Insert(this.fsInputLCMSFile.Name.LastIndexOf('.'), "_crunched");
                //StreamWriter wrd = new StreamWriter(wrd_name);
                string hdr = rdr.ReadLine();
                //hdr += ",composition,modification,mol_weight";
                //wrd.WriteLine(hdr);
                //string[] header = hdr.Split(',');
                // this.slQuantGAG = new SortedList<GAG, double>();

                // clear slQuantGAG
                this.sUnknownGAG.Clear();
                //for (int it = 0; it < this.slQuantGAG.Count; it++)
                //    this.slQuantGAG[this.slQuantGAG.Keys[it]] = 0.0;
                SortedList<double, List<string[]>> buffer = new SortedList<double,List<string[]>>();
                while (rdr.Peek() >= 0)
                {
                    string str = rdr.ReadLine();
                    string[] parts = str.Split(',');
                    double volume = -1.0 * Convert.ToDouble(parts[10]);
                    if (volume > -500.0)
                        continue;
                    if (buffer.ContainsKey(volume))
                    {
                        buffer[volume].Add(parts);
                    }
                    else
                    {
                        List<string[]> newlist = new List<string[]>();
                        newlist.Add(parts);
                        buffer.Add(volume, newlist);
                    }
                }
                foreach (var val in buffer)
                {
                    foreach (var line in val.Value)
                    {
                        string[] parts = line;
                        GAG g = new GAG(Convert.ToDouble(parts[6]), GAG_TOLERANCE);
                        //int index = this.alLookUpGAGTable.BinarySearch(g);
                        double volume = Convert.ToDouble(parts[10]);
                        int charge = Convert.ToInt16(parts[1]);
                        double fit = Convert.ToDouble(parts[4]);
                        int elutiontime = Convert.ToInt16(parts[0]);
                        double signalnoise = Convert.ToDouble(parts[9]);
                        double plus2regular = Convert.ToDouble(parts[11]) / volume;
                        double width = Convert.ToDouble(parts[8]);
                        //if (index < 0)
                        {
                            int index_unknown = this.sUnknownGAG.IndexOfKey(g);
                            if (index_unknown < 0)
                            {
                                g.mChargeState.Add(charge);
                                g.mFit.Add(fit);
                                g.mElutionTimes.Add(elutiontime);
                                g.mSignalNoise.Add(signalnoise);
                                g.mPlus2Regular.Add(plus2regular);
                                g.mWidth.Add(width);
                                g.mVolume.Add(volume);
                                g.mMW.Add(Convert.ToDouble(parts[6]));
                                g.mAvgMW.Add(Convert.ToDouble(parts[5]));
                                g.mGroup = 0.0;
                                this.sUnknownGAG.Add(g, volume);
                            }
                            else
                            {
                                this.sUnknownGAG[g] = this.sUnknownGAG[g] + volume;
                                if (((GAG)this.sUnknownGAG.Keys[index_unknown]).mChargeState.IndexOf(charge) < 0)
                                    ((GAG)this.sUnknownGAG.Keys[index_unknown]).mChargeState.Add(charge);
                                ((GAG)this.sUnknownGAG.Keys[index_unknown]).mFit.Add(fit);
                                ((GAG)this.sUnknownGAG.Keys[index_unknown]).mElutionTimes.Add(elutiontime);
                                ((GAG)this.sUnknownGAG.Keys[index_unknown]).mSignalNoise.Add(signalnoise);
                                ((GAG)this.sUnknownGAG.Keys[index_unknown]).mPlus2Regular.Add(plus2regular);
                                ((GAG)this.sUnknownGAG.Keys[index_unknown]).mWidth.Add(width);
                                ((GAG)this.sUnknownGAG.Keys[index_unknown]).mVolume.Add(volume);
                                ((GAG)this.sUnknownGAG.Keys[index_unknown]).mMW.Add(Convert.ToDouble(parts[6]));
                                ((GAG)this.sUnknownGAG.Keys[index_unknown]).mAvgMW.Add(Convert.ToDouble(parts[5]));
                            }

                            //wrd.WriteLine(str);
                            continue;
                        }
                        //g = (GAG)this.alLookUpGAGTable[index];
                        //index = this.slQuantGAG.IndexOfKey(g);
                        //double volume = Convert.ToDouble(parts[8]) * Convert.ToDouble(parts[10]);
                        // if (index < 0)
                        // {
                        //     this.slQuantGAG.Add(g, volume);
                        // }
                        // else
                        // {
                        //  this.slQuantGAG[g] = this.slQuantGAG[g] + volume;
                        /*str += ",\"";
                        str += g.Composition;
                        str += "\",\"";
                        str += g.Modification;
                        str += "\"";
                        str += ",\"";
                        str += g.MolecularWeight;
                        str += "\"";
                        wrd.WriteLine(str);*/

                        // }

                        //string[] gagName = parts[2].Replace("\"", "").Split('-');
                        //GAG gag = new GAG(gagName[0], gagName[1], parts[0], Convert.ToDouble(parts[1]), 20);
                        //this.alLookUpGAGTable.Add(gag);
                    }
                }

                //this.alLookUpGAGTable.Sort();
                this.saveAsToolStripMenuItem.Enabled = true;
                //wrd.Close();
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Error parsing LCMS Run File", "File Error",
                                 MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

            return false;

        }

        private FileStream fsInputLCMSFile;
        private FileStream fsGAGAnnotationFile;
        private ArrayList alLookUpGAGTable;
        private SortedList<GAG, double> slQuantGAG;
        private SortedList<GAG, double> sUnknownGAG;
        private SortedList<GAG, List<string>> hypList;
        private DataTable data;
        private bool bSum;
        private String massout = "";

        private void sumModToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.bSum = true;
        }

        private void unGroupedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Tab Separated Value (*.txt)|*.txt";
            dialog.ValidateNames = true;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                StreamWriter unknowns = new StreamWriter(dialog.FileName.Insert(dialog.FileName.LastIndexOf('.'), "_time"));
                
                //unknowns.WriteLine("AvgMonoMW\tAvgMassDiffAvgMonoMW\tVarAvgMW\tElution Time\tMinError\tAvgFitError\tMaxError\tMinfwhm\tAvgfwhm\tMaxfwhm\tMinPlus2Ratio\t" +
                  //"AvgPlus2Ratio\tMaxPlus2Ratio\tVarPlus2Ratio\tMinSignalNoise\tAvgSignalNoise\tTotalSignalNoise\tMaxSignalNoise\tNumCharges\tNumScans\tMinVolume\tAvgVolume\tTotalVolume\tMaxVolume\t");

                unknowns.WriteLine("avgMonoMW\tNumMod\tNumCharges\tNumScans\tDensity\tAvgPlus2Ratio\tTotalVolume\tCentroidScan");

                foreach (DictionaryEntry de in (IDictionary)this.sUnknownGAG)
                {
                    double denom = 0.0;
                    double num = 0.0;
                    for (int i = 0; i < ((GAG)de.Key).mMW.Count; i++)
                    {
                        double mw = ((GAG)de.Key).mMW.ElementAt(i);
                        double vol = ((GAG)de.Key).mVolume.ElementAt(i);
                        num += (mw * vol);
                        denom += vol;
                    }
                    double avgMonoMW = num / denom;
                    denom = 0.0;
                    num = 0.0;
                    for (int i = 0; i < ((GAG)de.Key).mElutionTimes.Count; i++)
                    {
                        double scan = (double) ((GAG)de.Key).mElutionTimes.ElementAt(i);
                        double vol = ((GAG)de.Key).mVolume.ElementAt(i);
                        num += (scan * vol);
                        denom += vol;
                    }
                    double CentroidScan = num / denom;
                    ((GAG)de.Key).mMolecularWeight = avgMonoMW;
                    double avgavgMW = ((GAG)de.Key).mAvgMW.Average();
                    int RangeofElution = ((GAG)de.Key).mElutionTimes.Max() - ((GAG)de.Key).mElutionTimes.Min();
                    double AvgError = ((GAG)de.Key).mFit.Average();
                    double Avgfwhm = ((GAG)de.Key).mWidth.Average();
                    double AvgPlus2Ratio = ((GAG)de.Key).mPlus2Regular.Average();
                    double AvgSignalNoise = ((GAG)de.Key).mSignalNoise.Average();
                    int NumCharges = ((GAG)de.Key).mChargeState.Count();
                    int NumScans = ((GAG)de.Key).mElutionTimes.Count();
                    double TotalSignalNoise = AvgSignalNoise * NumScans;
                    double TotalVolume = (double)de.Value;
                    int NumMod = 0;
                    for (int mods = 0; mods < ((GAG)de.Key).mModStates.Count(); mods++)
                    {
                        NumMod += ((GAG)de.Key).mModStates.ElementAt(mods).Value;
                    }
                    if (NumMod == 0) NumMod = 1;
                    // calculate and set density
                    if (((GAG)de.Key).mDensity <= 0.0)
                    {
                        double density = ((double)NumScans) / ((double)(RangeofElution + 1));
                        ((GAG)de.Key).mDensity = density;
                    }
                    double Density = ((GAG)de.Key).mDensity;

                    String output = "";
                    int features = 8;
                    for (int i = 0; i < features; i++)
                    {
                        output += "{";
                        output += i;
                        output += "}";
                        if (i != features-1)
                            output += "\t";
                    }

                    unknowns.WriteLine(output, avgMonoMW, NumMod, NumCharges, NumScans, Density, AvgPlus2Ratio, TotalVolume, CentroidScan);
                }

                unknowns.Close();

            }
        }

        private void groupedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // this.calMassShiftDeNovo(0.005, 3, 0.0, 90.0, 3);
            SortedList<double, int> shifts = new SortedList<double, int>();
            shifts.Add(17.02655,1);
            this.calMassShiftTargetted(shifts);
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Tab Separated Value (*.txt)|*.txt";
            dialog.ValidateNames = true;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                StreamWriter unknowns = new StreamWriter(dialog.FileName.Insert(dialog.FileName.LastIndexOf('.'), "_adducts"));
                StreamWriter scored = new StreamWriter(dialog.FileName.Insert(dialog.FileName.LastIndexOf('.'), "_scored"));
                //unknowns.WriteLine("avgMonoMW\tNumMod\tNumCharges\tNumScans\tTotalSignalNoise\tAvgError\tDensity\tTotalVolume");

                unknowns.WriteLine("avgMonoMW\tNumMod\tNumCharges\tNumScans\tDensity\tAvgPlus2Ratio\tTotalVolume\tCentroidScan");
                foreach (DictionaryEntry de in (IDictionary)this.sUnknownGAG)
                {
                    double denom = 0.0;
                    double num = 0.0;
                    for (int i = 0; i < ((GAG)de.Key).mMW.Count; i++)
                    {
                        double mw = ((GAG)de.Key).mMW.ElementAt(i);
                        double vol = ((GAG)de.Key).mVolume.ElementAt(i);
                        num += (mw * vol);
                        denom += vol;
                    }
                    double avgMonoMW = num / denom;
                    denom = 0.0;
                    num = 0.0;
                    for (int i = 0; i < ((GAG)de.Key).mElutionTimes.Count; i++)
                    {
                        double scan = (double)((GAG)de.Key).mElutionTimes.ElementAt(i);
                        double vol = ((GAG)de.Key).mVolume.ElementAt(i);
                        num += (scan * vol);
                        denom += vol;
                    }
                    double CentroidScan = num / denom;
                    ((GAG)de.Key).mCentroidScan = CentroidScan;
                    ((GAG)de.Key).mMolecularWeight = avgMonoMW;
                    double avgavgMW = ((GAG)de.Key).mAvgMW.Average();
                    int RangeofElution = ((GAG)de.Key).mElutionTimes.Max() - ((GAG)de.Key).mElutionTimes.Min();
                    double AvgError = ((GAG)de.Key).mFit.Average();
                    double Avgfwhm = ((GAG)de.Key).mWidth.Average();
                    double AvgPlus2Ratio = ((GAG)de.Key).mPlus2Regular.Average();
                    double AvgSignalNoise = ((GAG)de.Key).mSignalNoise.Average();
                    int NumCharges = ((GAG)de.Key).mChargeState.Count();
                    int NumScans = ((GAG)de.Key).mElutionTimes.Count();
                    double TotalSignalNoise = AvgSignalNoise * NumScans;
                    double TotalVolume = (double)de.Value;
                    int NumMod = 0;
                    for (int mods = 0; mods < ((GAG)de.Key).mModStates.Count(); mods++)
                    {
                        NumMod += ((GAG)de.Key).mModStates.ElementAt(mods).Value;
                    }
                    if (NumMod == 0) NumMod = 1;
                    // calculate and set density
                    if (((GAG)de.Key).mDensity <= 0.0)
                    {
                        double density = ((double)NumScans) / ((double)(RangeofElution + 1));
                        ((GAG)de.Key).mDensity = density;
                    }
                    double Density = ((GAG)de.Key).mDensity;

                    String output = "";
                    int features = 8;
                    for (int i = 0; i < features; i++) // 24
                    {
                        output += "{";
                        output += i;
                        output += "}";
                        if (i != features-1)
                            output += "\t";
                    }

                    // build data matrix for scoring
                    DataRow row = this.data.NewRow();
                    row[0] = avgMonoMW;
                    row[1] = NumCharges;
                    row[2] = NumMod;
                    row[3] = NumScans;
                    row[4] = Density;
                    row[5] = TotalVolume;
                    //row[6] = AvgPlus2Ratio;
                    //row[7] = TotalSignalNoise;
                    //row[8] = AvgError * -1.0;
                    //row[9] = Avgfwhm;
                    this.data.Rows.Add(row);

                    unknowns.WriteLine(output, avgMonoMW, NumMod, NumCharges, NumScans, Density, AvgPlus2Ratio, TotalVolume, CentroidScan);

                    /*unknowns.WriteLine(output, avgMonoMW, NumMod, Density, AvgMassDiff, VarAvgMW,
                        RangeofElution, MinError, AvgError, MaxError, Minfwhm, Avgfwhm, Maxfwhm, MinPlus2Ratio,
                        AvgPlus2Ratio, MaxPlus2Ratio, VarPlus2Ratio, MinSignalNoise, AvgSignalNoise, TotalSignalNoise,
                        MaxSignalNoise, NumCharges, NumScans, MinVolume, AvgVolume, TotalVolume, MaxVolume
                    );*/
                }
                this.scoreGAGs(scored);
                unknowns.Close();
                scored.Close();
            }
        }

        private void toQuanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Tab Separated Value (*.txt)|*.txt";
            dialog.ValidateNames = true;

            if (dialog.ShowDialog() == DialogResult.OK)
            {

                StreamWriter Quan = new StreamWriter(dialog.FileName.Insert(dialog.FileName.LastIndexOf('.'), "_unknowns"));

                Quan.WriteLine(massout);
                Quan.Close();
            }
         }

        private bool scoreGAGs(StreamWriter sw)
        {
            Dictionary<double, int> scores = new Dictionary<double, int>();
            foreach (var item in this.sUnknownGAG)
            {
                GAG g = (GAG)item.Key;
                double mw = g.mMolecularWeight;
                scores[mw] = 0;
            }
            for (int i = 1; i < this.data.Columns.Count; i++)
            {
                DataRow[] sortedrows = this.data.Select("", i + " DESC");
                int cur_pos = 0;
                int cur_val_pos = 0;
                int cur_score = 0;
                int max_score = this.data.Rows.Count + 1;
                foreach (var row in sortedrows)
                {
                    double cur_val = (double)(sortedrows[cur_val_pos].ItemArray[i]);
                    double val = (double)row[i];
                    if (cur_pos == 0 || val != cur_val)
                    {
                        cur_val_pos = cur_pos;
                        cur_score = cur_pos + 1;
                    }

                    scores[(double)row[0]] += max_score - cur_score;
                    cur_pos++;
                }
            }
            List<KeyValuePair<double, int>> scoresList = new List<KeyValuePair<double, int>>(scores);
            scoresList.Sort(
                delegate(KeyValuePair<double, int> firstPair,
                KeyValuePair<double, int> nextPair)
                {
                    return nextPair.Value.CompareTo(firstPair.Value);
                }
            );
            string sout = "Score\tMW\tHypothetical MW\tKey\tPPM\n";
            //int counter = 0;
            foreach (var val in scoresList)
            {
                //if (counter++ >= 150)
                //    break;
                GAG lookback = new GAG(val.Key, SEARCH_TOLERANCE);
                int ugidx = this.sUnknownGAG.IndexOfKey(lookback);
                if (ugidx < 0)
                {
                    //this shouldn't happen, but should be handled anyway
                    MessageBox.Show("ERROR: Couldn't find GAG with MW: " + val.Key);
                    continue;
                }
                lookback = this.sUnknownGAG.Keys[ugidx];
                lookback.score = val.Value;
                string ppm = "";
                GAG match_gag = new GAG(val.Key,MATCH_TOLERANCE);
                int idx = this.hypList.IndexOfKey(match_gag);
                if (idx >= 0)
                {
                    KeyValuePair<GAG, List<string>> kvp = this.hypList.ElementAt(idx);
                    double hyp_mw = kvp.Key.MolecularWeight;
                    lookback.match_mw = hyp_mw;
                    foreach (string str in kvp.Value)
                    {
                        lookback.match_string += (str + ", ");
                    }
                    lookback.match_string.TrimEnd(',');
                    double err = ((hyp_mw - val.Key) / hyp_mw) * 1000000;
                    ppm = String.Format("{0:0.000}", Math.Abs(err));
                }
                double denom = 0.0;
                double num = 0.0;
                double avgMonoMW = lookback.mMolecularWeight;
                double CentroidScan = lookback.mCentroidScan;
                int RangeofElution = lookback.mElutionTimes.Max() - lookback.mElutionTimes.Min();
                double AvgPlus2Ratio = lookback.mPlus2Regular.Average();
                double AvgSignalNoise = lookback.mSignalNoise.Average();
                int NumCharges = lookback.mChargeState.Count();
                int NumScans = lookback.mElutionTimes.Count();
                double TotalVolume = lookback.mVolume.Sum();
                int NumMod = 0;
                for (int mods = 0; mods < lookback.mModStates.Count(); mods++)
                {
                    NumMod += lookback.mModStates.ElementAt(mods).Value;
                }
                if (NumMod == 0) NumMod = 1;
                // calculate and set density
                
                double Density = lookback.mDensity;

                String output = "";
                int features = 8;
                for (int i = 0; i < features; i++) // 24
                {
                    output += "{";
                    output += i;
                    output += "}";
                    if (i != features - 1)
                        output += "\t";
                }

                sw.WriteLine(output, avgMonoMW, NumMod, NumCharges, NumScans, Density, AvgPlus2Ratio, TotalVolume, CentroidScan);

                sout += val.Value + "\t" + String.Format("{0:0.0000}", val.Key) + "\t" + match_mw + "\t" + match_key + "\t" + ppm + "\n";
            }
            sw.Write(sout);
            return true;
        }

      }


    /* Still buggy, de novo mass shift calculation
    private void calMassShiftDeNovo(double tolerance, int minFreq, double minShift, double maxShift, int numPossibleAdducts)
        { 
            SortedList< MassShift, HashSet<KeyValuePair<double, double> > > massShifts = new SortedList< MassShift, HashSet<KeyValuePair<double, double> > >();
            massShifts.Clear();
            double msTolerance = tolerance;
            foreach (var g1 in this.sUnknownGAG)
            {
                foreach (var g2 in this.sUnknownGAG) 
                {
                    GAG gag1 = (GAG)g1.Key;
                    GAG gag2 = (GAG)g2.Key;
                    if (gag1.CompareTo(gag2) >= 0)
                        continue;   // don't repeat comparisons and don't compare to same GAG
                    
                    
                    double mass1 = gag1.MolecularWeight;
                    double mass2 = gag2.MolecularWeight; 
                    double diff = Math.Abs(mass2 - mass1);

                    if (diff < minShift || diff > maxShift)
                        break;  // no need to check masses before minShift boundary or after the maxShift boundary
                
                    MassShift ms = new MassShift(diff, msTolerance);
                    
                    int indx = massShifts.IndexOfKey(ms);
                    if (indx < 0)
                    {
                        //insert new massShift
                        HashSet<KeyValuePair<double,double>> hset = new HashSet<KeyValuePair<double,double>>();
                        hset.Add(new KeyValuePair<double, double>(mass1, mass2));
                        ms.mMDList.Add(diff);
                        massShifts.Add(ms, hset);
                    }
                    else
                    {
                        //add to an existing massShift
                        massShifts.Keys[indx].mMDList.Add(diff);
                        ((HashSet<KeyValuePair<double,double>>) massShifts[ms]).Add(new KeyValuePair<double,double>(mass1,mass2));
                    }
                }
            }
            int count = massShifts.Count();
            MessageBox.Show(Convert.ToString(count));
            

            
            // Look through massShifts and "remove" shifts that can be described by other shifts. Removal means empty the hash set so its size is 0.
            foreach (var ms1 in massShifts)
            {
                foreach (var ms2 in massShifts)
                {
                    MassShift a = (MassShift)ms1.Key;
                    MassShift b = (MassShift)ms2.Key;

                    if (b.MolecularWeight <= a.MolecularWeight)
                        continue;

                    double div = b.MolecularWeight / a.MolecularWeight;
                    int int_div = (int)Math.Round(div);
                    double d_int_div = (double)int_div;
                    //double mult = ((double)div * b.MolecularWeight);
                    MassShift tmpA = new MassShift(div, b.Tolerance / a.MolecularWeight );
                    MassShift tmpB = new MassShift(d_int_div, b.Tolerance / a.MolecularWeight );
                    if (tmpA.CompareTo(tmpB) == 0 && int_div <= 2)
                    {
                        // divisor is an integer <= numPossibleAdducts
                        // empty the hash set corresponding to b (larger MW, ms2)
                        ms2.Value.Clear();
                    }
                }
            }
            

            // Take massShifts and sort it by the size of the hash set (the value)
            
            SortedList<int, List<int>> frequencyList = new SortedList<int, List<int>>();    // SortedList<size of hash set, index in massShifts>
            //String massout = "MassShif\tNums\n";
            massout += "MassShif\tNums\n";
            //Here write the first File to Quan

            foreach (var ms in massShifts)
            {
                int idx = massShifts.IndexOfKey(ms.Key);
                int size = ms.Value.Count();
                massout = massout + Convert.ToString(ms.Key.MolecularWeight);
                massout = massout + "\t";
                massout = massout + Convert.ToString(size);
                massout = massout + "\n";
                if (size < 10) 
                    continue;
                int pos = frequencyList.IndexOfKey(size);
                if (pos < 0)
                {
                    List<int> li = new List<int>();
                    li.Add(idx);
                    frequencyList.Add(size, li);
                }
                else
                {
                    frequencyList.ElementAt(pos).Value.Add(idx);
                }
            }

            MessageBox.Show(massout);
            
            String output = "Frequency\tMass Shift\n";
            foreach (var freq in frequencyList) {
                if (freq.Key < minFreq)
                    continue;
                output += Convert.ToString(freq.Key);
                foreach (var idx in freq.Value)
                {
                    output += "\t";
                    output += String.Format("{0:0.0000}", massShifts.Keys[idx].mMDList.Average());
                    //output += Convert.ToString(massShifts.Keys[idx].MolecularWeight);
                }
                output += "\n";
            }
            //int i = 0;
            //while (i < numShifts && frequencyList.Count() > i)
            //{
            //    int key = frequencyList.ElementAt(i).Key;
            //    List<int> vals = frequencyList.ElementAt(i).Value;
            //    if (key > 1)
            //    {
            //        foreach (var item in vals)
            //        {
            //            double mw = ((MassShift)massShifts.ElementAt((int)item).Key).MolecularWeight;
            //            output += frequencyList.ElementAt(i).Key;
            //            output += "\t";
            //            output += mw;
            //            output += "\n";
            //            i++;
            //        }

            //    }
            //}
            MessageBox.Show(output);


            //Put the massshift pair into series list
            double shift = 17.026;
            MassShift target = new MassShift(shift,0.01);
            HashSet<KeyValuePair<double, double>> mwList = new HashSet<KeyValuePair<double, double>>();

            foreach (var ms in massShifts) {
                if (ms.Key.CompareTo(target) == 0) {
                    mwList = ms.Value;
                    break;
                }
            }

            SortedList<double, List<double>> sList = new SortedList<double, List<double>>(); //series List of MW, the key is the last Molecular weight in the list.
            String pairs = "";
            foreach (var pair in mwList) {
                pairs += Convert.ToString(pair.Key);
                pairs += "\t";
                pairs += Convert.ToString(pair.Value);
                pairs += "\n";
                if (sList.ContainsKey(pair.Key))
                {
                    List<double> new_list = sList[pair.Key];
                    new_list.Add(pair.Value);
                    sList.Remove(pair.Key);
                    sList.Add(pair.Value, new_list);
                }
                else{
                    List<double> new_list = new List<double>();
                    new_list.Add(pair.Key);
                    new_list.Add(pair.Value);
                    sList.Add(pair.Value, new_list);
                }
            }
          MessageBox.Show(pairs);
          MessageBox.Show(Convert.ToString(sList.Count()));
          
          //display the series list and merge the GAG list
          //SortedList<GAG, double> groupedGAG = new SortedList<GAG, double>();
          //groupedGAG = this.sUnknownGAG;
          SortedList<double, List<double>> nList = new SortedList<double, List<double>>();
          foreach (var record in sList) {
              List<double> list = record.Value;
              double key = list.First();
              list.RemoveRange(0, 1);
              nList.Add(key,list);
          }

         foreach (var record in nList)
          {
              //Here also write another file for Quan.
              GAG fGAG = new GAG(record.Key, 20);
              int idx = this.sUnknownGAG.IndexOfKey(fGAG);
              fGAG = this.sUnknownGAG.ElementAt(idx).Key;
              KeyValuePair<double, int> mod = new KeyValuePair<double, int>(shift,record.Value.Count+1);
              fGAG.mModStates.Add(mod);
              int RangeofElution = fGAG.mElutionTimes.Max() - fGAG.mElutionTimes.Min() + 1;
              double densum = fGAG.mDensity * RangeofElution;
              double nusum = fGAG.mElutionTimes.Count();
              foreach (var item in record.Value)
              {
                GAG gGAG = new GAG(item, 20);
                int index = this.sUnknownGAG.IndexOfKey(gGAG);
                gGAG = this.sUnknownGAG.Keys[index];
                // Cumulative density
                RangeofElution = gGAG.mElutionTimes.Max() - gGAG.mElutionTimes.Min() + 1;
                double dsum = gGAG.mDensity * RangeofElution;
                double nsum = gGAG.mElutionTimes.Count();
                densum += dsum;
                nusum += nsum;
                
                // Charge states
                foreach (var charge in this.sUnknownGAG.Keys[index].mChargeState)
                {
                    if (fGAG.mChargeState.IndexOf(charge) < 0)
                        fGAG.mChargeState.Add(charge);
                }

                // Elution Times (duplicates okay)
                fGAG.mElutionTimes.AddRange(this.sUnknownGAG.Keys[index].mElutionTimes);

                // Volume
                fGAG.mVolume.Add(this.sUnknownGAG.ElementAt(index).Value);
                this.sUnknownGAG[fGAG] += this.sUnknownGAG.ElementAt(index).Value;

                // Remove GAG representing modified state from sUnknownGAG
                this.sUnknownGAG.RemoveAt(index);
              }
              fGAG.mDensity = nusum / densum;
          }
          pairs = "";
          foreach (var record in sList)
          {
              
              foreach (var item in record.Value)
              {

                  pairs += Convert.ToString(item);
                  pairs += "\t";
                  GAG fGAG = new GAG(item, 20);  //fGAG means find the GAG
                  int idx = this.sUnknownGAG.IndexOfKey(fGAG);
                  if (idx > 0)
                  {
                      ((GAG)this.sUnknownGAG.Keys[idx]).mGroup = shift;
                  }
                  else {
                      MessageBox.Show("Error Molecular Weight in the Mass Shift List!");
                  }
              }
              pairs += "\n";
          }
          MessageBox.Show(pairs);    
      

          //plot the one with mGroup flag.

        }*/

}
