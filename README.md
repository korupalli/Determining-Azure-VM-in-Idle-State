# Determining-Azure-VM-in-Idle-State
Using an Azure C# function to find idle virtual machines and send automatic notifications

## Bussiness Case
Virtual Machines can be expensive and often are left running unintentionally. Ideally if there if it is not in use they can be stopped and then restarted when needed.
- You will build a solution that will email someone when:
  - A VM is turned on or Stopped but not Deallocated
  - The VM has had no usage for 36 hours

## Solution

### Steps for setting up stack
- Register OperationalInsights in resource providers and Create a loganalytics workspace and configure the logs that you want to track 
- Install the extension on the VM you want to track by connecting to the VM in loganalytics portal
- Create a serice principle and add the ID's to KeyVault
- Create a Function Service and provide give access to keyVault and add the necessary varible to config settings(Code works for local VS code, change if you want to deploy)
- Create a logic app service with HTTP trigger and add the JSON of your requirement and in action, use gmail sothat when ever the http request is called, an action to send email is created. Collect the http link from logicapp designer and replace it in the code along with your json request

