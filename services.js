import { api } from "./api";

const API_ROOT = window.configSettings.apiRoot;

const services = {
  getDataById: id => {
    const config = {
      method: "GET",
      url: `${API_ROOT}/${id}`
    };

    return api.call(config);
  },
  postData: (data) => {
    const config = {
      method: "post",
      url: `${API_ROOT}/Data`,
      data: data
    };

    return api.call(config);
  },
};

export default services;
