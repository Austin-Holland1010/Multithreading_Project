using System;
using System.Threading;

namespace Project2
{ 
    public delegate void priceCutEvent(Int32 pr);

    public class Cruise
    {
        static Random rng = new Random(); //to generate random numbers
        public static event priceCutEvent priceCut; //link event to delegate
        private static Int32 cruisePrice = 200; //max price, will change based on pricecuts
        private Int32 processingNum = 0; //used to count the number of processingThreads
        private static int priceCutCount = 0;
        public Int32 getPrice()
        {
            return cruisePrice;
        }

        public static void pricingModel(Int32 price)
        {
            if (price < cruisePrice) //If a priceCut has occured
            {
                if (priceCut != null) //if the priceCut event has subscribers
                {
                    priceCut(price); //emit event to all the event subscribers
                }
                priceCutCount++;
            }
            cruisePrice = price;
        }

        public void cruiseFunction()
        {
            //Continue to generate new ticket prices
            //Once 20 price cuts happen the thread will terminate
            while(priceCutCount < 20)
            { 
                Thread.Sleep(500);
                Int32 price = rng.Next(40, 200);
                Console.WriteLine("New price per ticket is: ${0}", price);
                Cruise.pricingModel(price); //Update the price of a ticket in the Cruise class
            }
        }

        public void orderProcessing(OrderObject order)//method is called and used to start a processing thread
        {
            //The allowed range of credit card nums is all numbers
            //between 1000 and 1999

            if (order.getCardNo() >= 1000 || order.getCardNo() < 2000)
            {
                Cruise cruiseLiner = new Cruise();
                Thread cruise = new Thread(() => orderProcessingFunction(order));
                cruise.Start(); //Start a signle cruise thread
                cruise.Name = "processingThread#" + processingNum.ToString();
                processingNum++;
            }
            else 
            {
                Console.WriteLine("Incorrect Card Number"); //Shouldn't ever actually be printed
            }

        }

        public void orderProcessingFunction(OrderObject order)
        {
            //Variables to help make calculating the totalCost easier
            double totalCost = 0;
            double tax = 0;
            double locationCharge = 10.0;

            //Calcuations
            tax = (order.getUnitPrice() * 0.10);
            totalCost = order.getUnitPrice() * order.getQuantity();
            totalCost = totalCost + tax;
            totalCost = totalCost + locationCharge;

            //Get the name of the processing thread
            String processingThreadName = Thread.CurrentThread.Name;

            //Print to console
            Console.WriteLine("{0}: An order of {1} tickets at a unit cost of ${2} comes out to a total cost of ${3}"
                ,processingThreadName, order.getQuantity(), order.getUnitPrice(), totalCost); ;
            
        }
    }

    public class MultiCellBuffer
    {
        //create two buffer cells
        private OrderObject[] cells = new OrderObject[2];
        //create a cruise object
        private Cruise cruise = new Cruise();
        //Help the buffer to use both cells, with out this the threads would only use buffer cell 0
        Boolean used1stCell = false;

        public MultiCellBuffer()  //Constructor
        {
            //Fill the two buffer cells with non useful 'orders'
            //Will be replaced with good orders once
            //the priceCut event happens and ticket agents are notified
            OrderObject order1 = new OrderObject();
            order1.setID("");
            order1.setCardNo(0);
            order1.setQuantity(0);
            order1.setUnitPrice(0);
            cells[0] = order1;

            OrderObject order2 = new OrderObject();
            order2.setID("");
            order2.setCardNo(0);
            order2.setQuantity(0);
            order2.setUnitPrice(0);
            cells[1] = order2;
        }


        public void setOneBuff(OrderObject order)
        {
            //Use locks to determine which buffer cell is useable
            //I also included a boolean to force the threads to use the 2nd buffer cell
            if (Monitor.TryEnter(cells[0]) && used1stCell == false) //try to lock the 1st object in the array
            {
                try
                {
                    Console.WriteLine("Buffer Cell 0 used"); //Used to verify which cell is being used
                    used1stCell = true;

                    //save the object to the buffer cell
                    cells[0].setCardNo(order.getCardNo());
                    cells[0].setID(order.getID());
                    cells[0].setQuantity(order.getQuantity());
                    cells[0].setUnitPrice(order.getUnitPrice());

                    //send the cell to the Cruise's orderProcessing method
                    cruise.orderProcessing(cells[0]);
                }
                
                finally
                { 
                    Monitor.Exit(cells[0]); //unlock cells[0]
                }
            }
            else
            {
                Monitor.Enter(cells[1]);  //wait for the cells[1] lock to be avaiable
                try
                {
                    Console.WriteLine("Buffer Cell 1 used"); //used to verify the cell is being used
                    used1stCell = false;

                    //save the object to the buffer cell
                    cells[1].setCardNo(order.getCardNo());
                    cells[1].setID(order.getID());
                    cells[1].setQuantity(order.getQuantity());
                    cells[1].setUnitPrice(order.getUnitPrice());

                    //send the cell to the Cruise's orderProcessing method
                    cruise.orderProcessing(cells[1]);
                }
                finally //unlock cells[1] no matter what
                {
                    Monitor.Exit(cells[1]); 
                }
            }
        }

        
    }


    public class TicketAgent
    {
        private MultiCellBuffer buffer = new MultiCellBuffer(); //This buffer will be shared between the ticketAgents
        private static Semaphore _pool = new Semaphore(2,2); //This will allow the ticketAgents to know if the buffer is full
        public void ticketAgentFunction() //used to start the thread
        {
            
            Cruise cruise = new Cruise();
            for(Int32 x=0; x<16; x++)
            {
                Thread.Sleep(1000);
                Int32 price = cruise.getPrice();
                Console.WriteLine("TicketAgent{0} is selling tickets at a price of: ${1} each", Thread.CurrentThread.Name, price);
            }
        }

        public void ticketOnSale(Int32 price) //Event Handler
        {
            Random rng = new Random();
            Int32 cardNum = rng.Next(1000, 1999); //give the card num a valid number
            Thread thr = Thread.CurrentThread;
            Int32 quanity = rng.Next(20, 50);


            _pool.WaitOne();   //Wait until the buffer has an opening
            
            //Store all relevent data of the order into an order object
            OrderObject order = new OrderObject();
            order.setID(Thread.CurrentThread.Name);
            order.setCardNo(cardNum);
            order.setQuantity(quanity);
            order.setUnitPrice(price);

            buffer.setOneBuff(order); //Put the order object into a buffer cell

            _pool.Release();  //Once the order has been made free up the buffer count
        }
    }


    //The OrderObject Class with all the get/set methods listed in the assignmnet doc.
    public class OrderObject
    {
        private string senderID;
        private int cardNo;
        private int quantity;
        private double unitPrice;

        public OrderObject() //constructor
        {
        }

        public void setID(string threadID)
        {
            senderID = threadID;
        }
        public string getID()
        {
            return senderID;
        }

        public void setCardNo(int num)
        {
            cardNo = num;
        }
        public int getCardNo()
        {
            return cardNo;
        }

        public void setQuantity(int num)
        {
            quantity = num;
        }
        public int getQuantity()
        {
            return quantity;
        }

        public void setUnitPrice(double price)
        {
            unitPrice = price;
        }
        public double getUnitPrice()
        {
            return unitPrice;
        }
    }



    public class myApplication
    {
        static void Main(string[] args)
        {
            Cruise cruiseLiner = new Cruise();
            Thread cruise = new Thread(new ThreadStart(cruiseLiner.cruiseFunction));
            cruise.Start(); //Start a signle cruise thread
            cruise.Name = "cruise0";
            Console.WriteLine("{0} started", cruise.Name);

            TicketAgent ticketAgent = new TicketAgent();
            Cruise.priceCut += new priceCutEvent(ticketAgent.ticketOnSale);
            Thread[] ticketAgents = new Thread[5];
            for(int x=0; x<5; x++) //make 5 ticketAgent threads
            {
                ticketAgents[x] = new Thread(new ThreadStart(ticketAgent.ticketAgentFunction));
                ticketAgents[x].Start();
                ticketAgents[x].Name = (x + 1).ToString();
                Console.WriteLine("ticketAgent{0} started", ticketAgents[x].Name);
            }
        }
    }
}
