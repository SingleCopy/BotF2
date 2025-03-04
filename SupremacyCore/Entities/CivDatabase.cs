// CivDatabase.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using Supremacy.Collections;
using Supremacy.Game;
using Supremacy.Resources;
using Supremacy.Types;
using Supremacy.Utility;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Supremacy.Entities
{
    /// <summary>
    /// Represents the database of all the civilizations in the game, keyed by the
    /// <see cref="P:Supremacy.Entities.Civilization.CivID"/> property.
    /// </summary>
    [Serializable]
    public sealed class CivDatabase : KeyedCollectionBase<int, Civilization>
    {
        /// <summary>
        /// The default local path for the civilization database file.
        /// </summary>
        private const string DefaultDatabasePath = @"Resources\Data\Civilizations.xml";

        /// <summary>
        /// The set of XML schemas needed to validate the race database.
        /// </summary>
        private static XmlSchemaSet _xmlSchemas;

        private int _nextCivId;
        [NonSerialized]
        private Dictionary<string, Civilization> _reverseLookup;
        private static string _text;

        //private readonly  _appContext;

        /// <summary>
        /// Gets the <see cref="Civilization"/> corresponding to the specified unique key.
        /// </summary>
        /// <value>The civilization.</value>
        public Civilization this[string key]
        {
            get
            {
                lock (_reverseLookup)
                {
                    if (_reverseLookup.ContainsKey(key))
                    {
                        return _reverseLookup[key];
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Tries to get the Civilization with the key <paramref name="civKey"/>.
        /// </summary>
        /// <param name="civKey">The key.</param>
        /// <param name="value">The Civilization.</param>
        /// <returns><c>true</c> if the Civilization was successfully retrieved; otherwise, <c>false</c></returns>
        public bool TryGetValue(string civKey, out Civilization value)
        {
            lock (_reverseLookup)
            {
                if (_reverseLookup.ContainsKey(civKey))
                {
                    value = _reverseLookup[civKey];
                    return true;
                }
            }
            value = null;
            return false;
        }

        /// <summary>
        /// Determines whether this <see cref="CivDatabase"/> contains a <see cref="Civilization"/>
        /// with the specified unique key.
        /// </summary>
        /// <param name="key">The unique key.</param>
        /// <returns>
        /// <c>true</c> if this <see cref="CivDatabase"/> contains the corresponding
        /// <see cref="Civilization"/>; otherwise, <c>false</c>.
        /// </returns>
        public bool Contains(string key)
        {
            lock (_reverseLookup)
            {
                return _reverseLookup.ContainsKey(key);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CivDatabase"/> class.
        /// </summary>
        public CivDatabase()
            : base(o => (o.CivID == Civilization.InvalidID) ? (o.CivID = GameContext.Current.GenerateID()) : o.CivID)
        {
            _reverseLookup = new Dictionary<string, Civilization>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Inserts an element into the <see cref="T:System.Collections.ObjectModel.KeyedCollectionBase`2"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The object to insert.</param>
        protected override void InsertItem(int index, Civilization item)
        {
            base.InsertItem(index, item);
            lock (_reverseLookup)
            {
                _reverseLookup[item.Key] = item;
            }
        }

        /// <summary>
        /// Removes the element at the specified index of the <see cref="T:System.Collections.ObjectModel.KeyedCollectionBase`2"/>.
        /// </summary>
        /// <param name="index">The index of the element to remove.</param>
        protected override void RemoveItem(int index)
        {
            lock (_reverseLookup)
            {
                _ = _reverseLookup.Remove(Items[index].Key);
            }
            base.RemoveItem(index);
        }

        /// <summary>
        /// Removes all elements from the <see cref="T:System.Collections.ObjectModel.KeyedCollectionBase`2"/>.
        /// </summary>
        protected override void ClearItems()
        {
            base.ClearItems();
            lock (_reverseLookup)
            {
                _reverseLookup.Clear();
            }
        }

        /// <summary>
        /// Generates a new civilization ID.
        /// </summary>
        /// <returns>The new civilization ID.</returns>
        public int GetNewCivID()
        {
            return _nextCivId++;
        }

        /// <summary>
        /// Called after a <see cref="CivDatabase"/> is deserialized.
        /// </summary>
        /// <param name="context">The context.</param>
        [OnDeserialized]
        //  Zero References ??
        private void OnDeserialized(StreamingContext context)
        {
            if (_reverseLookup == null)
            {
                _reverseLookup = new Dictionary<string, Civilization>(StringComparer.OrdinalIgnoreCase);
            }

            lock (_reverseLookup)
            {
                foreach (Civilization civ in Items)
                {
                    _reverseLookup[civ.Key] = civ;

                    GameLog.Core.CivsAndRaces.DebugFormat("OnDeserialized: civ.Key = {0}", civ.Key);
                    if (civ.SpiedCivList != null)
                    {
                        foreach (Civilization spiedCiv in civ.SpiedCivList)
                        {
                            GameLog.Core.CivsAndRaces.DebugFormat("OnDeserialized: civ.Key = {0} spying on {1}", civ.Key, spiedCiv.Key);
                        }
                    }
                }
            }
        }

        public override void DeserializeOwnedData(IO.Serialization.SerializationReader reader, object context)
        {
            base.DeserializeOwnedData(reader, context);

            if (_reverseLookup == null)
            {
                _reverseLookup = new Dictionary<string, Civilization>(StringComparer.OrdinalIgnoreCase);
            }

            lock (_reverseLookup)
            {
                foreach (Civilization civ in Items)
                {
                    _reverseLookup[civ.Key] = civ;

                    // works 
                    _text = "de-serialize civ " + civ.Key;
                    //Console.WriteLine(_text);
                    GameLog.Core.SaveLoadDetails.DebugFormat(_text);
                    if (civ.SpiedCivList != null)
                    {
                        foreach (Civilization spiedCiv in civ.SpiedCivList)
                        {
                            _text = "DeserializeOwnedData: civ.Key= " + civ.Key
                                + ", spying on " + spiedCiv.Key;
                            //Console.WriteLine(_text);
                            GameLog.Core.SaveLoadDetails.DebugFormat(_text);
                        }
                    }

                }

            }
        }

        /// <summary>
        /// Saves the civilization database to XML.
        /// </summary>
        public void Save()
        {
            Save(ResourceManager.GetResourcePath(DefaultDatabasePath));
        }

        /// <summary>
        /// Saves the civilization database to XML.
        /// </summary>
        /// <param name="fileName">Name of the output file.</param>
        public void Save(string fileName)
        {
            XNamespace ns = XNamespace.Get("Supremacy:Civilizations.xsd");
            XNamespace supremacyNamespace = XNamespace.Get("Supremacy:Supremacy.xsd");

            XElement rootElement = new XElement(
                ns + "Civilizations",
                new XAttribute(
                    XNamespace.Xmlns + "s",
                    supremacyNamespace));

            XDocument xmlDoc = new XDocument(rootElement);

            foreach (Civilization civilization in this)
            {
                _ = civilization.AppendXml(rootElement);
            }

            xmlDoc.Save(fileName, SaveOptions.None);
        }

        /// <summary>
        /// Loads the schemas.
        /// </summary>
        private static void LoadSchemas()
        {
            _xmlSchemas = new XmlSchemaSet();
            _ = _xmlSchemas.Add("Supremacy:Supremacy.xsd",
                           ResourceManager.GetResourcePath("Resources/Data/Supremacy.xsd"));
            _ = _xmlSchemas.Add("Supremacy:Races.xsd",
                           ResourceManager.GetResourcePath("Resources/Data/Races.xsd"));
            _ = _xmlSchemas.Add("Supremacy:Civilizations.xsd",
                           ResourceManager.GetResourcePath("Resources/Data/Civilizations.xsd"));
        }

        /// <summary>
        /// Validates the XML.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ValidationEventArgs"/> instance containing the event data.</param>
        private static void ValidateXml(object sender, ValidationEventArgs e)
        {
            if (e.Severity == XmlSeverityType.Error)
            {
                throw new GameDataException(
                    e.Message,
                    DefaultDatabasePath);
            }
        }

        /// <summary>
        /// Loads the civilizaton database from XML.
        /// </summary>
        /// <returns></returns>
        public static CivDatabase Load()
        {
            GameLog.Core.GameData.Debug("Loading civilization database....");
            try
            {
                CivDatabase civDatabase = new CivDatabase();
                XNamespace ns = XNamespace.Get("Supremacy:Civilizations.xsd");
                XDocument xmlDoc = XDocument.Load(ResourceManager.GetResourcePath(DefaultDatabasePath));

                if (_xmlSchemas == null)
                {
                    LoadSchemas();
                }

                xmlDoc.Validate(_xmlSchemas, ValidateXml, true);



                foreach (XElement civElement in xmlDoc.Root.Elements(ns + "Civilization"))
                {
                    try
                    {
                        // for problems with Civilizations.xml active ...
                        //_text = "CivDatabase: adding civilization " + civElement.ToString();
                        //Console.WriteLine(_text);
                        // GameLog.Core.GameData.DebugFormat(_text);

                        civDatabase.Add(new Civilization(civElement));
                    }
                    catch (Exception e)
                    {
                        GameLog.Core.GameData.Error(string.Format("Problem adding civilization {0} to CivDatabase", civElement.ToString()), e);
                    }
                }
                GameLog.Core.GameData.Debug("Civilization database loaded");
                return civDatabase;
            }
            catch (SupremacyException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new SupremacyException(
                    "An error occurred while loading the Civilization Database: " + e.Message,
                    SupremacyExceptionAction.Disconnect);
            }
        }
    }
}
