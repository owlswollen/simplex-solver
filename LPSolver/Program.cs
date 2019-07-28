using System;

namespace LPSolver
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("Please give an MPS file path:");
                string path = Console.ReadLine();
                LPSolver problem = new LPSolver(path);
                if (problem.Solve() < 0)
                {
                    Console.Write("An error occured. Press a button to give another file: ");
                    Console.ReadKey(true);
                    continue;
                }
                while (true)
                {
                    Console.WriteLine("Press 0 to close the program, 1 to give another file, 2 to give an iteration number to print.");
                    ConsoleKeyInfo input = Console.ReadKey(true);
                    Console.WriteLine();
                    char inputChar = input.KeyChar;
                    if (inputChar == '0')
                    {
                        Environment.Exit(0);
                    }
                    else if(inputChar == '1')
                    {
                        break;
                    }
                    else if (inputChar == '2')
                    {
                        Console.WriteLine("Write the iteration you want to print.");
                        int iterationNo;
                        if (Int32.TryParse(Console.ReadLine(), out iterationNo))
                        {
                            problem.PrintIteration(iterationNo);
                        }
                        else
                        {
                            Console.WriteLine("Wrong input type.");
                        }
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine("Incorrect keystroke.");
                    }
                }
            }
        }
    }
}