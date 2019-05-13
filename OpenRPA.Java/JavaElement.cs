﻿using OpenRPA.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WindowsAccessBridgeInterop;

namespace OpenRPA.Java
{
    public class JavaElement : IElement
    {
        private const PropertyOptions DefaultPropertyOptions = PropertyOptions.AccessibleContextInfo |
              PropertyOptions.AccessibleIcons |
              PropertyOptions.AccessibleKeyBindings |
              PropertyOptions.AccessibleRelationSet |
              PropertyOptions.ParentContext |
              PropertyOptions.Children |
              PropertyOptions.ObjectDepth |
              PropertyOptions.TopLevelWindowInfo |
              PropertyOptions.ActiveDescendent |
              PropertyOptions.AccessibleText |
              PropertyOptions.AccessibleHyperText |
              PropertyOptions.AccessibleValue |
              PropertyOptions.AccessibleSelection |
              PropertyOptions.AccessibleTable |
              PropertyOptions.AccessibleTableCells |
              PropertyOptions.AccessibleTableCellsSelect |
              PropertyOptions.AccessibleActions;

        private AccessBridge _accessBridge;
        private AccessibleContextNode ac;
        private AccessibleNode c;
        public AccessibleContextInfo info = null;

        private JavaObjectHandle _ac;

        public string Name { get; set; }
        public string role { get; set; }
        public string path { get; set; }
        public string id { get; set; }
        public string title { get; set; }
        public int JvmId { get; set; }
        public int IndexInParent { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsVisible { get; set; }
        public Dictionary<string, object> properties { get; set; }
        public bool SupportInput
        {
            get
            {
                if (role == "menu" || role == "menu item") return false;
                if (info.accessibleText != 0)
                {
                    AccessibleTextItemsInfo textItems;
                    if (_accessBridge.Functions.GetAccessibleTextItems(JvmId, _ac, out textItems, 0))
                    {
                        return true;
                    }
                }
                if (info.accessibleSelection != 0)
                {
                    AccessibleTextSelectionInfo textItems;
                    if (_accessBridge.Functions.GetAccessibleTextSelectionInfo(JvmId, _ac, out textItems))
                    {
                        return true;
                    }

                }
                return false;
            }
        }
        public string value
        {
            get
            {
                if (info.accessibleText != 0)
                {
                    AccessibleTextItemsInfo textItems;
                    if (_accessBridge.Functions.GetAccessibleTextItems(JvmId, _ac, out textItems, 0))
                    {
                        var result = textItems.sentence;
                        if (result.EndsWith("\n")) result = result.Substring(0, result.Length - 1);
                        return result;
                    }
                }
                if (info.accessibleSelection != 0)
                {
                    AccessibleTextSelectionInfo textItems;
                    if (_accessBridge.Functions.GetAccessibleTextSelectionInfo(JvmId, _ac, out textItems))
                    {
                        var result = textItems.selectedText;
                        if (result.EndsWith("\n")) result = result.Substring(0, result.Length - 1);
                        return result;
                    }

                }
                return null;
            }
            set
            {
                if (info.accessibleText == 0) throw new Exception("Element does not support setting Text");
                AccessibleTextItemsInfo textItems;
                if (_accessBridge.Functions.GetAccessibleTextItems(JvmId, _ac, out textItems, 0))
                {
                    if (!_accessBridge.Functions.SetTextContents(JvmId, _ac, value))
                    {
                        throw new Exception("Error setting text");
                    }
                }

            }
        }
        public JavaElement(AccessibleNode c)
        {
            this.c = c;
            reload();
        }
        public void reload()
        {

            try
            {
                properties = new Dictionary<string, object>();
                var props = c.GetProperties(DefaultPropertyOptions);
                EnumProperties(props, null, 0, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.ToString());
            }
            if (properties.ContainsKey("role"))
            {
                role = (string)properties["role"];
            }
            id = role;

            if (properties.ContainsKey("name"))
            {
                Name = (string)properties["name"];
                id = role + "#" + Name;
            }
            else
            {
                var index = c.GetIndexInParent();
                if (index > -1)
                {
                    id = role + "(" + index.ToString() + ")";
                }
            }
            if (properties.ContainsKey("bounds"))
            {
                var bounds = properties["bounds"] as AccessibleRectInfo;
                if (bounds != null)
                {
                    X = bounds.X;
                    Y = bounds.Y;
                    Height = bounds.Height;
                    Width = bounds.Width;
                    IsVisible = bounds.IsVisible;
                }
            }

            try
            {

            }
            catch (Exception)
            {

                throw;
            }
            try
            {
                title = c.GetTitle();
                JvmId = c.JvmId;
                IndexInParent = c.GetIndexInParent();
                c.Refresh();
                ac = c as AccessibleContextNode;
                if (ac == null) return;
                info = ac.GetInfo();
                _accessBridge = c.AccessBridge;
                _ac = ac.AccessibleContextHandle;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.ToString());
            }
        }
        public void SetPath()
        {
            var paths = new List<string>();

            var nodepath = ac.BuildNodePath();
            foreach (var c in nodepath)
            {
                if (c is WindowsAccessBridgeInterop.AccessibleJvm) continue;
                var _ele = new JavaElement(c);
                paths.Add(_ele.id);
            }
            //var arr = paths.ToArray();
            //Array.Reverse(arr);
            path = string.Join(" > ", paths);

        }
        private void EnumProperties(PropertyList list, string prefix, int ident, bool ignoreGroups)
        {
            foreach (var prop in list)
            {
                var name = prop.Name.ToLower();
                if (name == "parent") continue;
                if (name == "children" && !ignoreGroups)
                {
                    var g = prop as PropertyGroup;
                    if (g == null) continue;
                    g.LoadChildren();
                    foreach (var c in g.Children)
                    {
                        var v = c.Value;
                        //_items.Add(new AccessibleContextNode(_accessBridge, c.Value));
                    }
                    continue;
                }
                if (name == "top level window") continue;
                if (!string.IsNullOrEmpty(prefix)) name = prefix + "." + name;
                if (prop is PropertyGroup)
                {
                    if (ignoreGroups) continue;
                    try
                    {
                        var g = prop as PropertyGroup;
                        if (g == null) continue;
                        g.LoadChildren();
                        if (ident <= 2) EnumProperties(g.Children, name, (ident + 1), false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine(name);
                        System.Diagnostics.Trace.WriteLine(ex.ToString());
                    }
                }
                else
                {
                    if (prop.Value == null) continue;
                    var value = prop.Value.ToString();
                    if (string.IsNullOrEmpty(value)) continue;
                    if (!properties.ContainsKey(name)) properties.Add(name, prop.Value);
                }
            }
        }
        public JavaElement Parent
        {
            get
            {
                if (ac == null) return null;
                var _parent = ac.GetParent();
                if (_parent == null) return null;
                return new JavaElement(_parent);
            }
        }
        public JavaElement[] Children
        {
            get
            {
                //if (Parent == null)
                //{
                //    c = java.javahook.jvms.Where(x => x.JvmId == c.JvmId).FirstOrDefault();
                //}
                //c.Refresh();
                var result = new List<JavaElement>();
                //if (ac == null) return result.ToArray();
                foreach (var c in this.c.GetChildren())
                {
                    result.Add(new JavaElement(c));
                }
                return result.ToArray();
            }
        }
        public void Click()
        {
            AccessibleActions _actions;

            var actions = new Dictionary<string, object>();
            if (_accessBridge.Functions.GetAccessibleActions(ac.JvmId, _ac, out _actions))
            {
                if (_actions.actionsCount > 0)
                {
                    foreach (var a in _actions.actionInfo)
                    {
                        if (actions.Count >= _actions.actionsCount) continue;
                        if (!string.IsNullOrEmpty(a.name))
                        {
                            if (!actions.ContainsKey(a.name))
                            {
                                actions.Add(a.name, a);
                                System.Diagnostics.Trace.WriteLine(a.name);
                            }
                        }
                    }
                }
            }
            if (actions.ContainsKey("click"))
            {
                int failure = 0;
                AccessibleActionInfo info = (AccessibleActionInfo)actions["click"];
                AccessibleActionsToDo actiontodo = new AccessibleActionsToDo();
                actiontodo.actions = new AccessibleActionInfo[32];
                actiontodo.actions[0] = info;
                actiontodo.actionsCount = 1;
                if (_accessBridge.Functions.DoAccessibleActions(ac.JvmId, _ac, ref actiontodo, out failure))
                {
                    System.Diagnostics.Trace.WriteLine("Success, click!");
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine("Click was NOT Successful!");
                }

            }
            else
            {
                System.Diagnostics.Trace.WriteLine("No click action attached to element!");
            }
        }
        public void Focus()
        {
            throw new NotImplementedException();
        }
        public void Highlight(bool Blocking, System.Drawing.Color Color, TimeSpan Duration)
        {
        }
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(title)) return title;
            return "id:" + id + " role:" + role + " Name: " + Name;
        }
        public override bool Equals(object obj)
        {
            var e = obj as JavaElement;
            if (e == null) return false;

            if (e.JvmId != JvmId) return false;
            if(!string.IsNullOrEmpty(path) && string.IsNullOrEmpty(e.path)) { e.SetPath(); }
            if (e.path != path) return false;
            if (e.id != id) return false;
            if (e.title != title) return false;
            if (e.IndexInParent != IndexInParent) return false;
            if (e.Name != Name) return false;
            if (e.role != role) return false;

            return true;
        }

    }
}
