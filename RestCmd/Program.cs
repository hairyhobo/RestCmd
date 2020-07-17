using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Newtonsoft.Json;
using RestSharp;

namespace RestCmd
{
    class RestTool 
    {
        private System.Collections.Specialized.NameValueCollection _dataCollection;
        private string token = "";
        private string tenant = "";
        private string credentials = "";
        private string baseURL = "https://cloud.mediusflow.com/";
        private DateTime expire = DateTime.Now;
        private string endpoint = "";
        public RestTool()
        {

            
            _dataCollection = ConfigurationManager.AppSettings;


        }

        class Program
        {
            //Here we go!
            public static void Main(string[] args)
            {


                string errorType = "";
                logger("Starting up");
                logger("Reading configuration file");
                RestTool r = new RestTool();
                logger("Read configuration file");
                logger("Checking directory " + r._dataCollection["RESTFolder"].ToString());
                r.CheckDir();

                DirectoryInfo dir = new DirectoryInfo(r._dataCollection["RESTFolder"]);
                
                logger("Files to process: " + dir.GetFiles("*.xml").Length);
                
                string previousTenant = "";

                foreach (var file in dir.EnumerateFiles())
                {
                    logger("*******************************************");
                    logger("Current file: " + file.Name);
                    logger("Finding credentials");
                    
                    //Get tenant name
                    r.getTenant(file.Name);
                    logger("Tenant based on filename: " + r.tenant);

                    //See if we have stored credentials for that tenant
                    if (r.tenant != previousTenant)
                    { 
                        r.FindCredentials(r.tenant);
                    }
                    
                    //If no credentials found skip the file 
                    if (r.credentials is null)
                    {
                        logger("Cannot find tenant credentials, skipping file: " +file.Name);
                        errorType = "No_Credentials";
                        HandleError(errorType,file.Name,dir.FullName);
                        logger("Done processing file");
                        logger("*******************************************");
                        continue;
                    }
                                      
                    logger("Credentials found for: " +r.tenant);

                    //get token if token is empty
                    if (r.token == "" || r.tenant != previousTenant || r.expire < System.DateTime.Now)
                    {
                        //logger("Client_ID: " + r.credentials.Substring(0, r.credentials.IndexOf(";")));
                        //logger("Client Secret: " + r.credentials.Substring(r.credentials.IndexOf(";")+1));
                        logger("Generating token");
                        r.getToken();
                        logger("Token generated: ");
                    }

                    XmlDocument doc = new XmlDocument();
                    try { 
                    doc.Load(file.FullName);
                    }
                    catch(Exception e) {
                        logger("Something went wrong while loading xml file: " + file.Name);
                        logger("Error: " + e.Message);
                        errorType = "Error_in_XML";
                        HandleError(errorType, file.Name,dir.FullName);

                        continue;
                    }
                    logger("XML file loaded, type is: " + doc.DocumentElement.Name);
                    logger("Sending data ");
                    
                    r.SendData(doc.DocumentElement.Name, doc.InnerXml);
                    logger("Data sent");

                    previousTenant = r.tenant;
                    ArchiveFile(file.Name,dir.FullName);
                    logger("Done processing file");
                    logger("*******************************************");
                }

                Console.WriteLine("Press enter to exit....");
                while (Console.ReadKey().Key != ConsoleKey.Enter) { }
            }

            static void logger(string msg)
            {
                Console.WriteLine(msg);
            }

            static void HandleError(string errorType,string fileName,string directoryPath)
            {
                if(!Directory.Exists(directoryPath + "\\Error\\"))
                {
                    Directory.CreateDirectory(directoryPath + "\\Error\\");
                }
                System.IO.File.Move(directoryPath + "\\" + fileName, directoryPath + "\\Error\\" + errorType + fileName);
            }

            static void ArchiveFile(string fileName,string directoryPath)
            {
                if (!Directory.Exists(directoryPath + "\\Archive\\"))
                {
                    Directory.CreateDirectory(directoryPath + "\\Archive\\");
                }
                System.IO.File.Move(directoryPath+"\\" + fileName, directoryPath + "\\Archive\\" + fileName);
            }

       
        }


        private void CheckDir()
        {
            DirectoryInfo dirInfo = new DirectoryInfo(_dataCollection["RESTFolder"]);
            if (dirInfo.Exists)
            {
                Console.WriteLine("Folder " + _dataCollection["RESTFolder"].ToString() + " exists");
                return;
            }
            if (!dirInfo.Exists)
            {
                Console.WriteLine("Folder " + _dataCollection["RESTFolder"].ToString() + " does not exists, creating folder");
                dirInfo.Create();
                Console.WriteLine("Created folder " + _dataCollection["RESTFolder"].ToString() + " please add files to it now");
                return;
            }
        }
        private void getTenant(string fileName)
        {
            fileName = fileName.Replace(".xml", "").ToLower();
            
            //Filename should start with tenantname for credentialcheck to work
            if (fileName.Contains("_"))
            {
                fileName = fileName.Substring(0, fileName.IndexOf("_"));
            }
            this.tenant = fileName;
        }
        private void FindCredentials(string tenant)
        {
            
            this.credentials =  this._dataCollection[tenant];

        }

        private void getToken()
        {
            
            var client = new RestClient(this.baseURL+this.tenant+this._dataCollection["Token"]);
            client.Timeout = -1;            
            var request = new RestRequest(Method.POST);
            request.AddHeader("Accept", "application/json");
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "client_credentials");
            request.AddParameter("scope", "Integration");
            request.AddParameter("client_id", this.credentials.Substring(0,this.credentials.IndexOf(";")));
            request.AddParameter("client_secret", this.credentials.Substring(this.credentials.IndexOf(";")+1));
           

            IRestResponse response = client.Execute(request);
            //var response = client.Execute(request);
            // Console.WriteLine(response.Content);
            Dictionary<string, string> values = JsonConvert.DeserializeObject<Dictionary<string, string>>(response.Content);
            this.token = values["access_token"];
            this.expire = System.DateTime.Now.AddSeconds(Convert.ToDouble(values["expires_in"]));
        }

        private void SendData(string type,string content)
        {
            switch(type)
            {
                case "ArrayOfPurchaseOrder":
                case "PurchaseOrder":
                    this.endpoint = _dataCollection["PurchaseOrder"];
                    break;
                case "ArrayOfAccrualTemplate":
                case "AccrualTemplate":
                    this.endpoint = _dataCollection["AccrualTemplate"];
                    break;
                case "ArrayOfCompany":
                case "Company":
                    this.endpoint = _dataCollection["Company"];
                    break;
                case "ArrayOfCurrency":
                case "Currency":
                    this.endpoint = _dataCollection["Currency"];
                    break;
                case "ArrayOfCurrencyRate":
                case "CurrencyRate":
                    this.endpoint = _dataCollection["CurrencyRate"];
                    break;
                case "ArrayOfDeliveryTerm":
                case "DeliveryTerm":
                    this.endpoint = _dataCollection["DeliveryTerm"];
                    break;
                case "ArrayOfDimensionValueConfiguration":
                case "DimensionValueConfiguration":
                    this.endpoint = _dataCollection["DimensionValueConfiguration"];
                    break;
                case "ArrayOfDimensionValue":
                case "DimensionValue":
                    this.endpoint = _dataCollection["DimensionValue"];
                    break;
                case "ArrayOfForbiddenListRestriction":
                case "ForbiddenListRestriction":
                    this.endpoint = _dataCollection["ForbiddenListRestriction"];
                    break;
                case "ArrayOfItem":
                case "Item":
                    this.endpoint = _dataCollection["Item"];
                    break;
                case "ArrayOfOptionalListRestriction":
                case "OptionalListRestriction":
                    this.endpoint = _dataCollection["OptionalListRestriction"];
                    break;
                case "ArrayOfPaymentTerm":
                case "PaymentTerm":
                    this.endpoint = _dataCollection["PaymentTerm"];
                    break;
                case "ArrayOfPeriod":
                case "Period":
                    this.endpoint = _dataCollection["Period"];
                    break;
                case "ArrayOfRangeRestriction":
                case "RangeRestriction":
                    this.endpoint = _dataCollection["RangeRestriction"];
                    break;
                case "ArrayOfRequiredListRestriction":
                case "RequiredListRestriction":
                    this.endpoint = _dataCollection["RequiredListRestriction"];
                    break;
                case "ArrayOfBasicRestriction":
                case "BasicRestriction":
                    this.endpoint = _dataCollection["BasicRestriction"];
                    break;
                case "ArrayOfRestrictionRuleGroup":
                case "RestrictionRuleGroup":
                    this.endpoint = _dataCollection["RestrictionRuleGroup"];
                    break;
                case "ArrayOfSupplierConfiguration":
                case "SupplierConfiguration":
                    this.endpoint = _dataCollection["SupplierConfiguration"];
                    break;
                case "ArrayOfSupplier":
                case "Supplier":
                    this.endpoint = _dataCollection["Supplier"];
                    break;
                case "ArrayOfTaxGroup":
                case "TaxGroup":
                    this.endpoint = _dataCollection["TaxGroup"];
                    break;
                case "ArrayOfUnit":
                case "Unit":
                    this.endpoint = _dataCollection["Unit"];
                    break;
                case "ArrayOfUserConfiguration":
                case "UserConfiguration":
                    this.endpoint = _dataCollection["UserConfiguration"];
                    break;
                case "ArrayOfUser":
                case "User":
                    this.endpoint = _dataCollection["User"];
                    break;
                default:

                    break;
            }
            if (this.endpoint != "")
            {
                string end = this.baseURL + this.tenant + this.endpoint;
                Console.WriteLine("sending data to:" + end);
                //Console.WriteLine(content);
                
                var client = new RestClient(this.baseURL + this.tenant + this.endpoint);
                client.Timeout = -1;
                var request = new RestRequest(Method.POST);
                request.AddHeader("Authorization", "Bearer " + this.token);
                request.AddHeader("Content-Type", "application/xml");
                request.AddParameter("application/xml", content, ParameterType.RequestBody);
                IRestResponse response = client.Execute(request);
                Console.WriteLine(response.Headers); 
                
            }
        }


    }
}
