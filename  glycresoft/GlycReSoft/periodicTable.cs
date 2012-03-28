using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlycreSoft
{
    class periodicTable
    {
        public periodicTable()
        {
            initiatePeriodicTable();
        }

        private void initiatePeriodicTable()
        {
            pTable.Add("H", 1.0078250350);
            pTable.Add("D", 2.014102);
            pTable.Add("He", 4.00260);
            pTable.Add("Li", 7.01600);
            pTable.Add("Be", 9.0122);
            pTable.Add("B", 11.00931);
            pTable.Add("C", 12.0000000000);
            pTable.Add("N", 14.0030740000);
            pTable.Add("O", 15.9949146300);
            pTable.Add("F", 18.9984032200);
            pTable.Add("Ne", 19.99244);
            pTable.Add("Na", 22.9897677000);
            pTable.Add("Mg", 23.98504);
            pTable.Add("Al", 26.98153);
            pTable.Add("Si", 27.97693);
            pTable.Add("P", 30.9737620000);
            pTable.Add("S", 31.9720707000);
            pTable.Add("Cl", 34.9688527300);
            pTable.Add("Ar", 39.96999);
            pTable.Add("K", 38.9637069000);
            pTable.Add("Ca", 39.9625912000);
            pTable.Add("Sc", 44.959404);
            pTable.Add("Ti", 45.952629);
            pTable.Add("V", 50.943962);
            pTable.Add("Cr", 52.940651);
            pTable.Add("Mn", 54.9380490000);
            pTable.Add("Fe", 55.934939);
            pTable.Add("Co", 58.933198);
            pTable.Add("Ni", 57.935346);
            pTable.Add("Cu", 62.939598);
            pTable.Add("Zn", 63.929145);
            pTable.Add("Ga", 68.925580);
            pTable.Add("Ge", 72.923463);
            pTable.Add("As", 74.921594);
            pTable.Add("Se", 75.919212);
            pTable.Add("Br", 78.9183361000);
            pTable.Add("Kr", 83.911507);
            pTable.Add("Rb", 84.911794);
            pTable.Add("Sr", 85.909267);
            pTable.Add("Y", 88.905849);
            pTable.Add("Zr", 89.904703);
            pTable.Add("Nb", 92.906377);
            pTable.Add("Mo", 99.907477);
            pTable.Add("Tc", 98.9062);
            pTable.Add("Ru", 95.907599);
            pTable.Add("Rh", 102.905500);
            pTable.Add("Pd", 105.903478);
            pTable.Add("Ag", 106.905092);
            pTable.Add("Cd", 115.904754);
            pTable.Add("In", 114.903880);
            pTable.Add("Sn", 118.903310);
            pTable.Add("Sb", 120.903821);
            pTable.Add("Te", 124.904433);
            pTable.Add("I", 126.9044730000);
            pTable.Add("Xe", 135.907214);
            pTable.Add("Cs", 132.9051);
            pTable.Add("Ba", 137.905232);
            pTable.Add("La", 138.906346);
            pTable.Add("Ce", 139.905433);
            pTable.Add("Pr", 140.907647);
            pTable.Add("Nd", 144.912570);
            pTable.Add("Pm", 147);
            pTable.Add("Sm", 149.917273);
            pTable.Add("Eu", 152.921225);
            pTable.Add("Gd", 157.924099);
            pTable.Add("Tb", 158.925342);
            pTable.Add("Dy", 163.929171);
            pTable.Add("Ho", 164.930319);
            pTable.Add("Er", 165.930290);
            pTable.Add("Tm", 168.934212);
            pTable.Add("Yb", 173.938859);
            pTable.Add("Lu", 174.940770);
            pTable.Add("Hf", 175.941406);
            pTable.Add("Ta", 180.947992);
            pTable.Add("W", 183.950928);
            pTable.Add("Re", 186.955744);
            pTable.Add("Os", 191.961467);
            pTable.Add("Ir", 192.962917);
            pTable.Add("Pt", 197.967869);
            pTable.Add("Au", 196.966543);
            pTable.Add("Hg", 203.973467);
            pTable.Add("Tl", 204.974401);
            pTable.Add("Pb", 207.976627);
            pTable.Add("Bi", 208.980374);
            pTable.Add("Po", 210);
            pTable.Add("At", 210);
            pTable.Add("Rn", 222);
            pTable.Add("Fr", 223);
            pTable.Add("Ra", 226);
            pTable.Add("Ac", 226);
            pTable.Add("Th", 232.038054);
            pTable.Add("Pa", 231);
            pTable.Add("U", 238.050784);
            pTable.Add("Np", 237);
            pTable.Add("Pu", 242);
            pTable.Add("Am", 243);
            pTable.Add("Cm", 247);
            pTable.Add("Bk", 247);
            pTable.Add("Cf", 249);
            pTable.Add("Es", 254);
            pTable.Add("Fm", 253);
            pTable.Add("Md", 256);
            pTable.Add("No", 254);
            pTable.Add("Lr", 257);
            pTable.Add("Av", 10);
            pTable.Add("Ub", 260);
            pTable.Add("Uc", 261);
            pTable.Add("Ud", 262);
            pTable.Add("Ue", 265);
            pTable.Add("Uf", 266);
        }
        
        public SortedList<string, double> pTable = new SortedList<string, double>();
    }
}
