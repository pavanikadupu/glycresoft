using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlycreSoft
{
    class GAG : IComparable
    {

        public GAG( double molecularweight, double tolerance)
        {
            this.mComposition = "";
            this.mAtomicComposition = "";
            this.mMolecularWeight = molecularweight;
            this.mModification = "";
            this.mTolerance = tolerance;
            this.mChargeState = new List<int>();
            this.mFit = new List<double>();
            this.mElutionTimes = new List<int>();
            this.mSignalNoise = new List<double>();
            this.mPlus2Regular = new List<double>();
            this.mWidth = new List<double>();
            this.mVolume = new List<double>();
            this.mMW = new List<double>();
            this.mAvgMW = new List<double>();
            this.mGroup = 0.0;
            this.mModStates = new List<KeyValuePair<double, int>>();
            this.group_flag = -1;
            this.match_string = "";
            this.match_mw = -1.0;
            this.score = -1;
            this.regressed_score = -1.0;
            this.mCentroidScan = -1.0;
            this.mCentroidScanError = -1.0;
            this.mPlus2Error = -1.0;
        }

        public GAG(string composition)
        {
            this.mComposition = composition;
            this.mAtomicComposition = "";
            this.mMolecularWeight = 0;
            this.mModification = "";
            this.mTolerance = 0;
            this.mChargeState = new List<int>();

        }

        public GAG(string composition, string modification, string atomiccomposition, double molecularweight, double tolerance)
        {
            this.mComposition = composition;
            this.mAtomicComposition = atomiccomposition;
            this.mMolecularWeight = molecularweight;
            this.mModification = modification;
            this.mTolerance = tolerance;
            this.mChargeState = new List<int>();
        }
        
        public GAG(string composition, string atomiccomposition, double molecularweight)
        {
            this.mComposition = composition;
            this.mAtomicComposition = atomiccomposition;
            this.mMolecularWeight = molecularweight;
            this.mModification = "";
            this.mTolerance = 0;
             this.mChargeState = new List<int>();
       }
        public GAG(string composition, string atomiccomposition, double molecularweight, double tolerance)
        {
            this.mComposition = composition;
            this.mAtomicComposition = atomiccomposition;
            this.mMolecularWeight = molecularweight;
            this.mModification = "";
            this.mTolerance = tolerance;
            this.mChargeState = new List<int>();
        }

        public int CompareTo (object gag)
        {
            // Use the smaller tolerance
            double tol = mTolerance;
            if (mTolerance > ((GAG)gag).mTolerance)
                tol = ((GAG)gag).mTolerance;

            if (Math.Abs((this.MolecularWeight - ((GAG)gag).MolecularWeight)/this.MolecularWeight) <= (tol / 1000000))
                return 0;
            int res = this.MolecularWeight.CompareTo((((GAG)gag).MolecularWeight));
            return res;
        }
        public double MolecularWeight
        {
            get { return this.mMolecularWeight; }
        }

        public string Composition
        {
            get { return this.mComposition; }
        }
        public string AtomicComposition
        {
            get { return this.mAtomicComposition; }
        }
        public string Modification
        {
            get { return this.mModification; }
        }
        public double Tolerance
        {
            get {return mTolerance;}
            set {mTolerance = Tolerance;}
        }


        public double mMolecularWeight;
        private string mComposition;
        private string mAtomicComposition;
        private string mModification;
        private double mTolerance;
        public List<int> mChargeState;
        public double mDensity = 0.0;
        public List<double> mFit;
        public List<int> mElutionTimes;
        public List<double> mPlus2Regular;
        public List<double> mVolume;
        public List<double> mWidth;
        public List<double> mSignalNoise;
        public List<double> mMW;
        public List<double> mAvgMW;
        public double mGroup;
        public List<KeyValuePair<double, int>> mModStates;
        public int group_flag;
        public string match_string;
        public double match_mw;
        public int score;
        public double regressed_score;
        public double mCentroidScan;
        public double mCentroidScanError;
        public double mPlus2Error;
    }

    class MassShift : IComparable
    {
        public MassShift(double molecularweight, double tolerance)
        {
            this.mMolecularWeight = molecularweight;
            this.mTolerance = tolerance;
            this.mMDList = new List<double>();
        }

        public int CompareTo(object massshift)
        {
            if(Math.Abs((this.MolecularWeight - ((MassShift)massshift).MolecularWeight)) < mTolerance * 2)
                    return 0;
            int res = this.MolecularWeight.CompareTo((((MassShift)massshift).MolecularWeight));
            return res;
        }

        public double MolecularWeight
        {
            get { return this.mMolecularWeight;}
        }

        public double Tolerance
        {
            get { return mTolerance; }
            set { mTolerance = Tolerance; }
        }

        private double mMolecularWeight;
        private double mTolerance;
        public List<double> mMDList;
    }
}
