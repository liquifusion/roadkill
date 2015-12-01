// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StructureMapDependencyScope.cs" company="Web Advanced">
// Copyright 2012 Web Advanced (www.webadvanced.com)
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Http.Dependencies;
using Microsoft.Practices.ServiceLocation;
using StructureMap;
using StructureMap.Pipeline;
using IDependencyResolver = System.Web.Mvc.IDependencyResolver;

namespace Roadkill.Core.DependencyResolution.StructureMap
{
	public class StructureMapServiceLocator : ServiceLocatorImplBase, IDependencyResolver, System.Web.Http.Dependencies.IDependencyResolver
	{
		private const string NestedContainerKey = "Nested.Container.Key";
		public IContainer Container { get; set; }
		public bool IsWeb { get; set; }

		public IContainer CurrentNestedContainer
		{
			get
			{
				if (IsWeb)
				{
					return (IContainer) HttpContext.Items[NestedContainerKey];
				}
				else
				{
					return (IContainer) Thread.GetData(Thread.GetNamedDataSlot(NestedContainerKey));
				}
			}
			set
			{
				if (IsWeb)
				{
					HttpContext.Items[NestedContainerKey] = value;
				}
				else
				{
					Thread.SetData(Thread.GetNamedDataSlot(NestedContainerKey), value);
				}
			}
		}

		private HttpContextBase HttpContext
		{
			get
			{
				var ctx = Container.TryGetInstance<HttpContextBase>();
				return ctx ?? new HttpContextWrapper(System.Web.HttpContext.Current);
			}
		}

		public StructureMapServiceLocator(IContainer container, bool isWeb)
		{
			if (container == null)
			{
				throw new ArgumentNullException("container");
			}

			IsWeb = isWeb;
			Container = container;

			if (!IsWeb)
			{
				CreateNestedContainer();
			}
		}

		public void CreateNestedContainer()
		{
			if (CurrentNestedContainer != null)
			{
				return;
			}
			CurrentNestedContainer = Container.GetNestedContainer();
		}

		public void Dispose()
		{
			DisposeNestedContainer();
			Container.Dispose();
		}

		public void DisposeNestedContainer()
		{
			if (CurrentNestedContainer != null)
			{
				CurrentNestedContainer.Dispose();
				CurrentNestedContainer = null;
			}
		}

		public void RegisterType<T>(T instance)
		{
			CurrentNestedContainer.Configure(x => x.For<T>().AddInstance(new ObjectInstance(instance)));
		}

		public IEnumerable<object> GetServices(Type serviceType)
		{
			return DoGetAllInstances(serviceType);
		}

		protected override IEnumerable<object> DoGetAllInstances(Type serviceType)
		{
			return (CurrentNestedContainer ?? Container).GetAllInstances(serviceType).Cast<object>();
		}

		protected override object DoGetInstance(Type serviceType, string key)
		{
			IContainer container = (CurrentNestedContainer ?? Container);

			if (string.IsNullOrEmpty(key))
			{
				return serviceType.IsAbstract || serviceType.IsInterface
					? container.TryGetInstance(serviceType)
					: container.GetInstance(serviceType);
			}

			return container.GetInstance(serviceType, key);
		}

		#region WebApi IDependencyResolver
		public IDependencyScope BeginScope()
		{
			IContainer child = Container.GetNestedContainer();
			return new StructureMapServiceLocator(child, true);
		}
		#endregion
	}
}
