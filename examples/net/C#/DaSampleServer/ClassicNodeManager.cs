#region Copyright (c) 2011-2021 Technosoftware GmbH. All rights reserved
//-----------------------------------------------------------------------------
// Copyright (c) 2011-2021 Technosoftware GmbH. All rights reserved
// Web: https://technosoftware.com 
// 
// License: 
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
// SPDX-License-Identifier: MIT
//-----------------------------------------------------------------------------
#endregion Copyright (c) 2011-2021 Technosoftware GmbH. All rights reserved

#region	Using Directives
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ServerPlugin.Contracts;
using System.Net.Http.Json;

#endregion

namespace ServerPlugin
{

    /// <summary>
    /// OPC Server Configuration and IO Handling
    ///
    /// This C# based plugin for the OPC Server .NET Classic Edition shows a base 
    /// OPC 2.05a / 3.00 server implementation  
    /// At startup items with several data types and access rights are statically 
    /// defined. 
    /// The RefreshThread simulates signal changes for the items
    ///      SimulatedData.Ramp
    ///      SimulatedData.Random
    ///      SimulatedData.Sine
    /// and writes the changed values into the internal cache. The generic server
    /// read the item values from device as required through calling the function 
    /// ReadItem().
    /// Item values written by a client are written into the local buffer only.
    /// </summary>
    public class ClassicNodeManager : ClassicBaseNodeManager
    {
        #region Constants

        #region Data Access IDs

        const int PropertyIdCasingMaterial          = 5650;
        const int PropertyIdCasingHeight            = 5651;
        const int PropertyIdCasingManufacturer      = 5652;

        #endregion

        #endregion

        #region Fields

        private static int itemHandle_ = 1;

        // Simulated Data Items
        private static MyItem myDynamicRampItem_;
        private static MyItem myDynamicSineItem_;
        private static MyItem myDynamicRandomItem_;

        #endregion

        #region Static Fields

        static private Thread myThread_;
        static private ManualResetEvent stopThread_;

        #endregion

        #region Signal State Data

        // DATA DEFINITIONS
        // Important: All data needs to be defined as STATIC.
        // This is important because this class is used in multiple instances.

        private static readonly Dictionary<IntPtr, MyItem> Items = new Dictionary<IntPtr, MyItem>();
        private static Dictionary<string, MyItem> _itemsWithName = new Dictionary<string, MyItem>();
        #endregion

        #region General Methods (not related to an OPC specification)

        #region  .NET API Generic Server Default Methods
        //---------------------------------------------------------------------
        //  .NET API Methods 
        // (Called by the generic server)
        //---------------------------------------------------------------------

        /// <summary>
        /// Gets the logging level to be used.
        /// </summary>
        /// <returns>
        ///     A LogLevel
        /// </returns>
        public override int OnGetLogLevel()
        {
            return (int)LogLevel.Info;
        }

        /// <summary>
        /// Gets the logging path to be used.
        /// </summary>
        /// <returns>
        ///     Path to be used for logging.
        /// </returns>
        public override string OnGetLogPath()
        {
            return "";
        }
		
        /// <summary>
        /// 	<para>
        ///         This method is called from the generic server at the startup; when the first
        ///         client connects or the service is started. All items supported by the server
        ///         need to be defined by calling the <see cref="AddItem">AddItem</see> or
        ///         <see cref="ClassicBaseNodeManager.AddAnalogItem">AddAnalogItem</see> callback method for each item.
        ///     </para>
        /// 	<para>The Item IDs are fully qualified names ( e.g. Dev1.Chn5.Temp ).</para>
        /// 	<para>
        ///         If <see cref="DaBrowseMode">DaBrowseMode.Generic</see> is set the generic
        ///         server part creates an approriate hierarchical address space. The sample code
        ///         defines the application item handle as the buffer array index. This handle is
        ///         passed in the calls from the generic server to identify the item. It should
        ///         allow quick access to the item definition / buffer. The handle may be
        ///         implemented differently depending on the application.
        ///     </para>
        /// 	<para>The branch separator character used in the fully qualified item name must
        ///     match the separator character defined in the OnGetDAServerParameters method.</para>
        /// </summary>
        /// <returns>A <see cref="StatusCodes"/> code with the result of the operation.</returns>
        public override int OnCreateServerItems()
        {
            // create a thread for simulating signal changes
            // in real application this thread reads from the device
            myThread_ = new Thread(RefreshThread) { Name = "Device Simulation", Priority = ThreadPriority.AboveNormal };
            myThread_.Start();

            return StatusCodes.Good;
        }

        public override void OnShutdownSignal()
        {
            //////////////////  TO-DO  /////////////////
            // close the device communication

            // terminate the simulation thread
            stopThread_ = new ManualResetEvent(false);
            stopThread_.WaitOne(5000, true);
            stopThread_.Close();
            stopThread_ = null;
        }

        public override int OnGetDaServerParameters(out int updatePeriod, out char branchDelimiter, out DaBrowseMode browseMode)
        {
            // Default Values
            updatePeriod = 100;                             // ms
            branchDelimiter = '.';
            browseMode = DaBrowseMode.Generic;            // browse the generic server address space
            return StatusCodes.Good;
        }

        #endregion

        #endregion

        #region Data Access related Methods

        #region  .NET API Generic Server Default Methods
        //---------------------------------------------------------------------
        //  .NET API Methods 
        // (Called by the generic server)
        //---------------------------------------------------------------------

        public override ClassicServerDefinition OnGetDaServerDefinition()
        {
            DaServer = new ClassicServerDefinition
            {
                ClsIdApp = "{FB2A8966-6B9B-4205-BDE3-18D87B80240B}",
                CompanyName = "Technosoftware GmbH",
                ClsIdServer = "{C08E1A10-A3DF-41F1-BA42-745B0004C8D0}",
                PrgIdServer = "OpcNetDaAe.DaSimpleSample",
                PrgIdCurrServer = "OpcNetDaAe.DaSimpleSample.90",
                ServerName = "OPC Server SDK .NET DA Simple Sample Server",
                CurrServerName = "OPC Server SDK .NET DA Simple Sample Server V9.0"
            };

            return DaServer;
        }

        /// <summary>
        /// Query the properties defined for the specified item
        /// </summary>
        /// <param name="deviceItemHandle">Generic Server device item handle</param>
        /// <param name="noProp">Number of properties returned</param>
        /// <param name="iDs">Array with the the property ID number</param>
        /// <returns>A <see cref="StatusCodes" /> code with the result of the operation. 
        ///  StatusCodes.Bad if the item has no custom properties.</returns>
        public override int OnQueryProperties(
            IntPtr deviceItemHandle,
            out int noProp,
            out int[] iDs)
        {
            MyItem item;
            if (Items.TryGetValue(deviceItemHandle, out item))
            {
                if (item.ItemProperties != null)
                {
                    // item has  custom properties
                    noProp = item.ItemProperties.Length;
                    iDs = new int[noProp];
                    for (int i = 0; i < noProp; ++i)
                    {
                        iDs[i] = item.ItemProperties[i].PropertyId;
                    }
                    return StatusCodes.Good;
                }
            }
            noProp = 0;
            iDs = null;
            return StatusCodes.Bad;
        }

        /// <summary>
        /// Returns the values of the requested custom properties of the requested item. This
        /// method is not called for the OPC standard properties 1..8. These are handled in the
        /// generic server.
        /// </summary>
        /// <returns>HRESULT success/error code. Bad if the item has no custom properties.</returns>
        /// <param name="deviceItemHandle">Generic Server device item handle</param>
        /// <param name="propertyId">ID of the property</param>
        /// <param name="propertyValue">Property value</param>
        public override int OnGetPropertyValue(IntPtr deviceItemHandle, int propertyId, out object propertyValue)
        {
            MyItem item;
            if (Items.TryGetValue(deviceItemHandle, out item))
            {
                if (item.ItemProperties != null)
                {

                    int numProp = item.ItemProperties.Length;
                    for (int i = 0; i < numProp; ++i)
                    {
                        if (item.ItemProperties[i].PropertyId == propertyId)
                        {
                            propertyValue = item.ItemProperties[i].PropertyValue;
                            return StatusCodes.Good;
                        }
                    }
                }
            }
            // Item property is not available
            propertyValue = null;
            return StatusCodes.BadInvalidPropertyId;
        }

        /// <summary>
        /// 	<para>This method is called when a client executes a 'write' server call. The items
        ///     specified in the DaDeviceItemValue array need to be written to the device.</para>
        /// 	<para>The cache is updated in the generic server after returning from the
        ///     customization WiteItems method. Items with write error are not updated in the
        ///     cache.</para>
        /// </summary>
        /// <returns>A <see cref="StatusCodes"/> code with the result of the operation.</returns>
        /// <param name="values">Object with handle, value, quality, timestamp</param>
        /// <param name="errors">Array with HRESULT success/error codes on return.</param>
        public override int OnWriteItems(DaDeviceItemValue[] values, out int[] errors)
        {
            errors = new int[values.Length];                            // result array
            for (int i = 0; i < values.Length; ++i)                     // init to Good
                errors[i] = StatusCodes.Good;

            // TO-DO: write the new values to the device
            foreach (DaDeviceItemValue t in values)
            {
                MyItem item;
                if (Items.TryGetValue(t.DeviceItemHandle, out item))
                {
                    // Only if there is a Value specified write the value into buffer
                    if (t.Value != null)
                        item.Value = t.Value;
                    if (t.QualitySpecified)
                        item.Quality = new DaQuality(t.Quality);
                    if (t.TimestampSpecified)
                        item.Timestamp = t.Timestamp;
                }
            }
            return StatusCodes.Good;
        }

        #endregion

        #endregion

        #region Create Data Access Sample Variants

        #endregion

        #region Refresh Thread

        // This method simulates item value changes.
        void RefreshThread()
        {
            for (; ; )   // forever thread loop
            {
                Thread.Sleep(1000);    // ms
                var responseTags = GetTagsAsync().GetAwaiter().GetResult();
                var tags = ConvertToMyItemArr(responseTags);
                foreach (var item in tags)
                {
                    if (!_itemsWithName.ContainsKey(item.ItemName))
                    {
                        AddItem(item.ItemName, DaAccessRights.ReadWritable, item.Value, out item.DeviceItemHandle);
                        Items.Add(item.DeviceItemHandle, item);
                        _itemsWithName.Add(item.ItemName, item);
                        SetItemValue(item.DeviceItemHandle, item.Value, item.Quality.Code, DateTime.Now);
                    }
                }

                var responseTagsForRemove = GetTagsForRemoveAsync().GetAwaiter().GetResult();
                ClearRemovedTagsList().GetAwaiter().GetResult();
                var tagsForRemove = ConvertToMyItemArr(responseTagsForRemove);
                foreach (var item in tagsForRemove)
                {
                    if (_itemsWithName.ContainsKey(item.ItemName))
                    {
                        var intPtrRemovedItem = _itemsWithName[item.ItemName].DeviceItemHandle;
                        RemoveItem(intPtrRemovedItem);
                        Items.Remove(intPtrRemovedItem);
                        _itemsWithName.Remove(item.ItemName);
                    }
                }
                

                if (stopThread_ != null)
                {
                    stopThread_.Set();
                    return;               // terminate the thread
                }
            }
        }

        private static async Task<IEnumerable<Tag>> GetTagsAsync()
        {
            try
            {
                var client = new HttpClient();
                var response = await client.GetFromJsonAsync<List<Tag>>("https://localhost:7174/api/Tags/GetAllTags");
                client.Dispose();
                return response;
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"Error: {e}");
            }

            return null;
        }

        private static async Task<IEnumerable<Tag>> GetTagsForRemoveAsync()
        {
            try
            {
                var client = new HttpClient();
                var response = await client.GetFromJsonAsync<List<Tag>>("https://localhost:7174/api/Tags/GetRemovedTags");
                client.Dispose();
                return response;
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"Error: {e}");
            }

            return null;
        }

        private static async Task ClearRemovedTagsList()
        {
            try
            {
                var client = new HttpClient();
                await client.PostAsync("https://localhost:7174/api/Tags/ClearRemovedTagsList", new StringContent(""));
                client.Dispose();
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"Error: {e}");
            }
        }

        private static IEnumerable<MyItem> ConvertToMyItemArr(IEnumerable<Tag> tags)
        {
            return tags.Select(ConvertToMyItem);
        }

        private static MyItem ConvertToMyItem(Tag tag)
        {
            if (int.TryParse(tag.TagValue, out var intValue))
            {
                return new MyItem(tag.TagName, intValue)
                {
                    Quality = DaQuality.Good
                };
            }
            if (float.TryParse(tag.TagValue, out var floatValue))
            {
                return new MyItem(tag.TagName, floatValue)
                {
                    Quality = DaQuality.Good
                };
            }

            if (bool.TryParse(tag.TagValue, out var boolValue))
            {
                return new MyItem(tag.TagName, boolValue)
                {
                    Quality = DaQuality.Good
                };
            }

            return new MyItem(tag.TagName, tag.TagValue)
            {
                Quality = DaQuality.Good
            };
        }
        
        #endregion

    }

    #region My Item Property Class

    /// <summary>
    /// My Item Property Implementation
    /// </summary>
    public class MyItemProperty
    {
        #region Constructors, Destructor, Initialization

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="propertyId">ID of the property</param>
        /// <param name="propertyValue">Value of the property</param>
        public MyItemProperty(int propertyId, object propertyValue)
        {
            PropertyId = propertyId;
            PropertyValue = propertyValue;
        }

        #endregion

        #region Properties

        /// <summary>
        /// ID of the property
        /// </summary>
        public int PropertyId { get; private set; }

        /// <summary>
        /// Value of the property
        /// </summary>
        public object PropertyValue { get; private set; }

        #endregion

    }

    #endregion

    #region My Item Class

    /// <summary>
    /// My Item Implementation
    /// </summary>
    class MyItem
    {

        #region Constructors, Destructor, Initialization

        public MyItem(
                        string itemName,
                        object initValue)
        {
            ItemName = itemName;
            Value = initValue;
            Quality = DaQuality.Good;
            Timestamp = DateTime.UtcNow;
        }

        public MyItem(
                string itemName,
                object initValue,
                MyItemProperty[] itemProperties)
        {
            ItemName = itemName;
            Value = initValue;
            Quality = DaQuality.Good;
            ItemProperties = itemProperties;
            Timestamp = DateTime.UtcNow;
        }

        #endregion

        #region Properties

        // Can be used to identify the item, not used in this example. You can use also other information like device
        // specific information (e.g. serial line, datablock and data number for PLC, ...
        public IntPtr DeviceItemHandle;
        public string ItemName { get; private set; }
        public object Value { get; set; }
        public DaQuality Quality { get;  set; }
        public DateTime Timestamp { get; set; }
        public MyItemProperty[] ItemProperties { get; private set; }

        #endregion
    }

    #endregion

}