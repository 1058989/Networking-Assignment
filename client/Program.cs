using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MessageNS;



//Authors:
//Giovanni Vonk  1058989
//Marcel Boot  1038695

class Program
{
    static void Main(string[] args)
    {
        ClientUDP cUDP = new ClientUDP();
        cUDP.Start();
    }
}

class ClientUDP
{
    private Socket sock;
    private EndPoint remoteEP;

    private Message? recentMessage;

    private List<string> textList;

   
    private int longTimeout;

    bool sendTime;

    public ClientUDP()
    {
        IPAddress ipAddress = IPAddress.Parse(GetMyIP());
        IPEndPoint serverEndpoint = new IPEndPoint(ipAddress, 32000);
        remoteEP = (EndPoint)serverEndpoint;
        longTimeout = 5000;
        textList = new List<string>();
        sendTime = true;
        try
        {
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
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
        sock.Close();
        Environment.Exit(1);
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
        
        //this will send the hello
        SendMessage(MessageType.Hello, "20");

        //this will receive the welcome
        ReceiveMessage(longTimeout);
        //Checks if the received type is the same as the expected (welcome)
        CorrectType(MessageType.Welcome);
        //Checks it the content of the received type is the same as expected
        CorrectContent("");
       
        //this wil send the request data.
        SendMessage(MessageType.RequestData, "hamlet.txt");

        
        
        while(sendTime)
        {
            
        
            //Receives the data/ and Endt from the server
            ReceiveMessage(longTimeout);

            //if the received message is end, then leave while loop.
            if(recentMessage!.Type == MessageType.End)
            {
                //Checks it the received content is same as expected : ""
                CorrectContent("");
                //break out the loop.
                sendTime= false;
                break;
            }

            //Checks if the received type is the same as the expected (data)
            CorrectType(MessageType.Data);
            //Checks if the received content is the same as expected
            CorrectContent();

            
           //Used for testing to see what happens if a data gets send late.
           //Thread.Sleep(54);
          
           //Sends the ack message to the server.
           SendMessage(MessageType.Ack, recentMessage.Content!.Substring(0,4));
        }
        
        
        //writes the hamlet in pieces to the terminal
        // string justhamlet = "";
        // foreach(var item in textList)
        // {
        //     justhamlet = justhamlet + item;
        // }
        // Console.WriteLine(justhamlet);

        Console.WriteLine(" \n \n \nEnd \n \n \n");
        sock.Close();

    }

    //send a message to the server
    private void SendMessage(MessageType messageType, string content)
    {
        byte[] msg = ToEncodedJson(messageType, content);
        try
        {
            sock.SendTo(msg, msg.Length, SocketFlags.None, remoteEP);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error sending message: " + ex.Message);
            Environment.Exit(1);
        }
    }

    //Receive a message from the server and also print it in the console.
    private void ReceiveMessage(int timeout = -1)
    {
        byte[] buffer = new byte[10000];

        try
        {
            sock.ReceiveTimeout = timeout;
            int bytesReceived = sock.ReceiveFrom(buffer, ref remoteEP);
            string data = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
            Message receivedMessage = JsonSerializer.Deserialize<Message>(data)!;
            recentMessage = receivedMessage;
            //if the type is error, close the connection and exit.
            if (receivedMessage.Type== MessageType.Error)
            {
                Console.WriteLine("Error: "+receivedMessage.Content);
                sock.Close();
                Environment.Exit(1);
                
            }

            //if the type is data, then store the received data somewhere.
            if(receivedMessage.Type == MessageType.Data)
            {
                
                
                var secondPart = receivedMessage.Content!.Substring(4);
                textList.Add(secondPart);
                

            }
            
            
            Console.WriteLine($"\n\n\nMessage received from  Server {remoteEP}: {receivedMessage.Type}  {receivedMessage.Content}");
           
        }
        catch (SocketException ex) 
        {
            if (ex.SocketErrorCode == SocketError.TimedOut)
            {
               HandleError($"Long Timeout: No activity for {timeout} milliseconds ");
            }
            else
            {  
               HandleError("SocketExecption => " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error receiving message: " + ex.Message);
            Environment.Exit(1);
           
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

   // Get the IP
    private string GetMyIP()
    {
        string myIP = string.Empty;
        try
        {
            // Get the host name
            string hostName = Dns.GetHostName();
            Console.WriteLine("Host Name: " + hostName);
            
            // Get the IP address list for the host
            IPAddress[] addresses = Dns.GetHostAddresses(hostName);
            
            // Find the first IPv4 address
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
            Environment.Exit(1);
        }
        return myIP;
    }
}

   


