using System;
using System.Data.SqlTypes;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MessageNS;
using System.Diagnostics;

//Authors:
//Giovanni Vonk  1058989
//Marcel Boot  1038695


// Do not modify this class
class Program
{
    static void Main(string[] args)
    {
        ServerUDP sUDP = new ServerUDP();
        sUDP.Start();
        Console.WriteLine("hey");
    }
}

class ServerUDP
{
    private Socket sock;
    private EndPoint remoteEP;
    private Message? recentMessage;

    private string? textdata;
    private int maxThreshold;

    private int shortTimeout;
   

    public ServerUDP()
    {
        
        IPAddress ipAddress = IPAddress.Parse(GetMyIP());
        IPEndPoint localEndpoint = new IPEndPoint(ipAddress, 32000);
        remoteEP = new IPEndPoint(IPAddress.Any, 0);
        maxThreshold = 0;
        shortTimeout = 1000;
        
        

        try
        {
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sock.Bind(localEndpoint);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Socket Error: " + ex.Message);
            Environment.Exit(1);
        }
    }


     //Handles error and inform the other party.
    private void HandleError(string content)
    {
        
        SendMessage(MessageType.Error, content);
        Console.WriteLine(content);
        Start();
        
    }
    
    //Checks if the received type is the same as the expected type.
    private void CorrectType(MessageType myType)
    {
        if(recentMessage!.Type != myType)
        {
            HandleError($"Expected type {myType} is NOT the same as received type {recentMessage.Type}");
        }
        else
        {
            Console.WriteLine($"Expected type {myType} is the same as received type {recentMessage.Type}");
        }
    }

    private void CorrectContent(string content = "")
    {
        //checks if the content of Data is the same as expected (expected example: "0001Act I\n ...")
        //works with Ack too.
        if(recentMessage!.Type == MessageType.Data || recentMessage.Type == MessageType.Ack)
        {
            //if the first 4 symbols if message type data can be converted to int, it means it has a valid content.
            try
            {
                string firstfour = recentMessage.Content!.Substring(0,4);
                int testInt = 0;
                testInt = Int32.Parse(firstfour);
                //In case the ID is equal to 0000 (somehow)
                if(testInt <= 0)
                {   
                    HandleError("Error with the content of the data: the id of the message is 0000" );
                }
                else
                {
                    Console.WriteLine($"Content: {recentMessage.Content} of type: {recentMessage.Type} is the same as the expected content");
                }

            }
            catch(Exception){
                HandleError($"Content: {recentMessage.Content} of type: {recentMessage.Type} is Not the same as expected content");
            }
        }
        //checks if the content of message type hello is the same as expected (expected:  number in a form of string)
        else if(recentMessage.Type == MessageType.Hello)
            {
                //if you are able to parse the content, it means 
                try
                {
                    int test = Int32.Parse(recentMessage.Content!);
                    //check if positive because you cant sent negative amount of numbers.
                    if(test <= 0)
                    {
                        HandleError($"Content of Hello should be a positive number");
                    }
                    else
                    {
                        Console.WriteLine($"Content: {recentMessage.Content} of type: {recentMessage.Type} is the same as the expected content");
                    }
                    
                }
                catch(Exception)
                {
                    HandleError($"Content: {recentMessage.Content} of type: {recentMessage.Type} is Not the same as the expected content");
                }
            }
        //The rest of the types.
        else
        {
            if(recentMessage.Content == content)
            {
              Console.WriteLine($"Content: {recentMessage.Content} of type: {recentMessage.Type} is the same as expected Content: {content}");
            }
            else
            {
              HandleError($"Content: {recentMessage.Content} of type: {recentMessage.Type} is Not the same as expected Content: {content}");
            }
        }
        
    }

    public void Start()
    {
        
 
        Console.WriteLine("Server started. Waiting for messages...");

        //this wil receive the hello
        ReceiveMessage();
        // this will check if the received type is the same as the expected  type (hello)
        CorrectType(MessageType.Hello);
        //this will check if the received content is the same as expected
        CorrectContent();

        //this wil send the welcome
        SendMessage(MessageType.Welcome, "");
            
        //this will receive the requestData
        ReceiveMessage();
       
        // this will check if the received type is the same as the expected  type (requestData)
        CorrectType(MessageType.RequestData);
        //this will check if the received content is the same as expected (only works if "hamlet.txt")
        CorrectContent("hamlet.txt");
        
        //this reads the text given in last message and stores it.
        TextReader(recentMessage!.Content!);

        int hamletStringLength = textdata!.Length;
        Console.WriteLine("hamlet.txt has a string count of " + hamletStringLength);

        int dataMessageID= 1;
        int dataMessageReceivedID = 1;
        int currentDataAmount = 1;
        int dataSize = 200;
        int receiveLoop = 0;
        string correct = "0001";
        List<string> lastSentIds = new();
        while(true )
        {
            
           
           //if there isnt any hamlet string left (nothing to read), break out the while loop.
           if(hamletStringLength==0)
           {
            break;
           }
            Console.WriteLine($"I will be sending {currentDataAmount} messages of size {dataSize}");
            //only send new data if there isnt anything left te be received.
            if(receiveLoop == 0)
            {
                for(int i = 0; i < currentDataAmount; i++)
               {
                  string dataID = FormatNumber(dataMessageID);
                  if (textdata.Length < dataSize)
                   {
                     dataSize = textdata.Length; 
                   }
                   //if there isnt any hamlet string left (nothing to read), break out the for loop.
                  if(hamletStringLength==0)
                  {
                   break;
                  }
                  //Combines values for the content to be sent to client.
                  string currentData=  textdata.Substring(0,dataSize);
                  textdata = textdata.Substring(dataSize);
                  string contentToBeSent = dataID+currentData;
            
                  //Sends the data message to the client.
                  SendMessage(MessageType.Data, contentToBeSent);

                  lastSentIds.Add(dataID);
                  hamletStringLength = hamletStringLength-currentData.Length;
                  dataMessageID= dataMessageID + 1;
                  receiveLoop = receiveLoop + 1;
               }
            }
          
            
            
            var receiveCount = 0;
            bool goneWrong = false;
            List<string> receivedIds = new();
            Stopwatch shortTimeoutStopwatch = new Stopwatch();
            shortTimeoutStopwatch.Start();
            while(receiveCount < currentDataAmount)
            {
                
        
                //short time out handling
                if (shortTimeoutStopwatch.ElapsedMilliseconds >= shortTimeout)
                {
                  shortTimeoutStopwatch.Stop();
                
                  dataMessageID = Int32.Parse(correct);
                  goneWrong = true;
                  Console.WriteLine("1 second has passed. SHORT TIME OUT \n");
                  break; 
                }


                //Receive the message
                ReceiveMessage();
                // this will check if the received type is the same as the expected  type (ACK)
                CorrectType(MessageType.Ack);
                //this will check if the received content is the same as expected
                CorrectContent();
                receivedIds.Add(recentMessage.Content!);

            
               
                //checks if the received id is the same as the id that was last sent.
                if(receivedIds[receiveCount]==lastSentIds[receiveCount])
                {
                    Console.WriteLine($"from Server: {lastSentIds[receiveCount]} is equal to from Client: {receivedIds[receiveCount]} \n");
                    if(!goneWrong)
                    {
                      correct= receivedIds[receiveCount];
                      dataMessageReceivedID++; 
                    }
                   
                    
                }
                else
                {
                    Console.WriteLine("Oh no! an ACK has missed");
                    Console.WriteLine($"from Server: {lastSentIds[receiveCount]} is NOT equal to from Client: {receivedIds[receiveCount]} \n");
                    goneWrong = true;
                }

                receiveCount++;
                
                //if all the dataid's received are the same as the dataid's sent
                if(receiveCount==lastSentIds.Count && goneWrong == false)
                {
                    Console.WriteLine("Correct acknowledgments received.");
                    break;
                }
                
            }

            shortTimeoutStopwatch.Stop();
            receiveLoop = receiveLoop-receiveCount;
            lastSentIds = lastSentIds.Skip(lastSentIds.Count - receiveLoop).ToList();
            dataMessageID = dataMessageReceivedID;

            //doubles the amount of data sent . if threshold is met then threshold it will be.
            if(goneWrong)
            {
                currentDataAmount = 1;
            }
            else
            {
                if(currentDataAmount * 2 < maxThreshold)
                {
                    currentDataAmount = currentDataAmount * 2;
                }
                else
                {
                    currentDataAmount = maxThreshold;
                }
            }
            

        }
        

        //Sends the end message to the client
        SendMessage(MessageType.End, "");

       
       
    
        Console.WriteLine($"\n \n End \n \n");
        Start();
        sock.Close();
        Console.WriteLine("Server closed.");
    }


    //Receive a message from the client and also print it in the console.
    private void ReceiveMessage( int TimeOut = -1)
    {
        
        byte[] buffer = new byte[10000];
        string data;
        try
        {
            int bytesReceived = sock.ReceiveFrom(buffer, ref remoteEP);
            data = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
            Message receivedMessage = JsonSerializer.Deserialize<Message>(data)!;
            recentMessage = receivedMessage;
            //if the message type is error, then restart the application.
            if (receivedMessage.Type== MessageType.Error)
            {
                Console.WriteLine("Error: "+receivedMessage.Content);
                Start();
                return;
            }

            //if the message type is hello, then set the max threshold.
            if(receivedMessage.Type == MessageType.Hello)
            {
                
                CorrectContent();
                int max = Int32.Parse(receivedMessage.Content!);
                maxThreshold = max;
               
            }
            Console.WriteLine($"Message received from Client {remoteEP}: {receivedMessage.Type}  {receivedMessage.Content}");
        }
        catch (SocketException ex) 
        {
            if (ex.SocketErrorCode == SocketError.TimedOut)
            {
               HandleError($"Long Timeout: No activity for {TimeOut} milliseconds ");
            }
            else
            {  
               HandleError("SocketExecption => " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            HandleError("Error receiving message: " + ex.Message);
        }
    }

    //send a message to the client.
    private void SendMessage(MessageType messageType, string content)
    {
        byte[] msg = ToEncodedJson(messageType, content);
        try
        {
           // Console.WriteLine(msg.Length);
            sock.SendTo(msg, msg.Length, SocketFlags.None, remoteEP);
        }
        catch (Exception ex)
        {
            
            Console.WriteLine("Error sending message: " + ex.Message);
        }
    }

    //Encodes the message to JSON so it is suitable for travel (will be deserialezed).
    private byte[] ToEncodedJson(MessageType messageType, string content)
    {
        Message message = new Message
        {
            Type = messageType,
            Content = content
        };
        string jsonMessage = JsonSerializer.Serialize(message);
        return Encoding.UTF8.GetBytes(jsonMessage);
    }

    //reads the text and stores it.
    private void TextReader(string textFile)
    {
        //currently this file reads the textfile from the bin (in visual studio).
        string text = File.ReadAllText(textFile);
        textdata = text;
    }
    
    static string FormatNumber(int number) => number.ToString("D4");
    // Get the IP
    private string GetMyIP()
    {
        string myIP = string.Empty;
        try
        {
            // Get the host name
            string hostName = Dns.GetHostName();
            Console.WriteLine("Host Name: " + hostName);
            
            // Get the IP address list from the host.
            IPAddress[] addresses = Dns.GetHostAddresses(hostName);
            
           
            foreach (IPAddress address in addresses)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    myIP = address.ToString();
                    Console.WriteLine("My IP Address: " + myIP);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error getting IP address: " + ex.Message);
        }
        return myIP;
    }
}


